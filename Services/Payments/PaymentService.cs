using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments.Gateways;
using RROMS.Web.Services.Payments.Notifications;

namespace RROMS.Web.Services.Payments;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IStripeGateway _stripe;
    private readonly IPaymentOrderProvider _orders;
    private readonly IPaymentQueries _queries;
    private readonly IEnumerable<IPaymentNotifier> _notifiers;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        AppDbContext db,
        IStripeGateway stripe,
        IPaymentOrderProvider orders,
        IPaymentQueries queries,
        IEnumerable<IPaymentNotifier> notifiers,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _stripe = stripe;
        _orders = orders;
        _queries = queries;
        _notifiers = notifiers;
        _logger = logger;
    }

    // ------------------------------------------------------------------------------------
    // Customer checkout
    // ------------------------------------------------------------------------------------

    public async Task<PaymentResult> StartCheckoutAsync(int orderId, int customerUserId, string method, string baseUrl)
    {
        if (!PaymentMethod.CheckoutChoices.Contains(method))
        {
            throw new PaymentException("Please choose how you would like to pay.");
        }

        // 1. The order must exist and belong to this customer. The amount always comes from the
        //    order, never from the browser.
        var order = await _orders.GetOrderAsync(orderId);
        if (order is null || order.CustomerUserId != customerUserId)
        {
            throw new PaymentException("We couldn't find that order.");
        }

        if (order.TotalAmount <= 0)
        {
            throw new PaymentException("This order has nothing to pay.");
        }

        // 2. An order has at most one payment row (unique index). Create it, or re-open the old one.
        var existing = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == orderId);

        Payment payment;
        if (existing is null)
        {
            payment = new Payment
            {
                OrderId = order.OrderId,
                Amount = order.TotalAmount,
                PaymentMethod = method,
                PaymentStatus = PaymentStatus.Pending
            };

            _db.Payments.Add(payment);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Two clicks at once hit the unique "one payment per order" index.
                _logger.LogWarning(ex, "Could not create a payment for order {OrderId}", orderId);
                throw new PaymentException("A payment for this order is already in progress.");
            }
        }
        else
        {
            payment = await ReopenAsync(existing, order, method);
        }

        // 3a. Pay at the counter: nothing more to do until staff take the money.
        if (method == PaymentMethod.PayAtCounter)
        {
            return new PaymentResult { Payment = payment };
        }

        // 3b. Online: send the customer to Stripe's hosted page.
        try
        {
            var session = await _stripe.CreateCheckoutSessionAsync(
                payment,
                order.CustomerEmail,
                successUrl: $"{baseUrl}/Payments/Success?paymentId={payment.PaymentId}",
                cancelUrl: $"{baseUrl}/Payments/Cancel?paymentId={payment.PaymentId}&attempt={payment.CreatedAt.Ticks}");

            // Remember the Stripe session, so this attempt can be closed or recognised later.
            await _db.Payments
                .Where(p => p.PaymentId == payment.PaymentId && p.PaymentStatus == PaymentStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.TransactionRef, session.SessionId));

            return new PaymentResult { Payment = payment, RedirectUrl = session.Url };
        }
        catch (PaymentException)
        {
            // Stripe said no (or isn't configured) - don't leave a pending row waiting for nothing.
            await MarkFailedAsync(payment.PaymentId);
            throw;
        }
    }

    /// <summary>
    /// Starts a new attempt on the order's existing payment row. A paid or refunded payment is final;
    /// a pending one is retired first (its Stripe page is closed); then the row is claimed with one
    /// conditional UPDATE so two simultaneous clicks cannot both start an attempt.
    /// </summary>
    private async Task<Payment> ReopenAsync(Payment existing, PaymentOrderInfo order, string method)
    {
        if (existing.PaymentStatus == PaymentStatus.Paid)
        {
            throw new PaymentException("This order has already been paid.");
        }

        if (existing.PaymentStatus == PaymentStatus.Refunded)
        {
            throw new PaymentException("This order's payment was refunded, so it can't be paid again here. Please speak to the restaurant.");
        }

        if (existing.PaymentStatus == PaymentStatus.Pending)
        {
            // Close the old Stripe page first so it can't be paid after we replace it.
            if (!string.IsNullOrEmpty(existing.TransactionRef)
                && existing.PaymentMethod != PaymentMethod.PayAtCounter
                && !await _stripe.TryExpireSessionAsync(existing.TransactionRef))
            {
                // The customer may have just paid in another tab - don't create a second charge.
                throw new PaymentException("Your previous payment is still being processed. Please wait a moment and refresh.");
            }

            await _db.Payments
                .Where(p => p.PaymentId == existing.PaymentId && p.PaymentStatus == PaymentStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.PaymentStatus, PaymentStatus.Failed));
        }

        DateTime now = DateTime.Now;   // same local clock as SYSDATETIME()

        // The CK_Payments_PaidAt constraint needs PaidAt empty for pending/failed, so it is cleared in the same UPDATE.
        var claimed = await _db.Payments
            .Where(p => p.PaymentId == existing.PaymentId && p.PaymentStatus == PaymentStatus.Failed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.PaymentStatus, PaymentStatus.Pending)
                .SetProperty(p => p.PaymentMethod, method)
                .SetProperty(p => p.Amount, order.TotalAmount)
                .SetProperty(p => p.CreatedAt, now)
                .SetProperty(p => p.PaidAt, (DateTime?)null)
                .SetProperty(p => p.RecordedByStaffId, (int?)null)
                .SetProperty(p => p.TransactionRef, (string?)null)
                .SetProperty(p => p.StripePaymentIntentId, (string?)null));

        if (claimed == 0)
        {
            throw new PaymentException("A payment for this order is already in progress.");
        }

        return await _db.Payments.AsNoTracking().FirstAsync(p => p.PaymentId == existing.PaymentId);
    }

    // ------------------------------------------------------------------------------------
    // Stripe results (called by the webhook)
    // ------------------------------------------------------------------------------------

    public async Task<bool> MarkPaidOnlineAsync(int paymentId, string? paymentIntentId)
    {
        DateTime? paidAt = DateTime.Now;

        // One conditional UPDATE: if Stripe sends the same event twice, the second call updates
        // 0 rows and is ignored - no double receipts. A "failed" row is accepted too: if Stripe says
        // the money arrived, that wins (for example the customer paid just as we retired the attempt).
        var updated = await _db.Payments
            .Where(p => p.PaymentId == paymentId
                     && (p.PaymentStatus == PaymentStatus.Pending || p.PaymentStatus == PaymentStatus.Failed)
                     && p.PaymentMethod != PaymentMethod.PayAtCounter)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.PaymentStatus, PaymentStatus.Paid)
                .SetProperty(p => p.PaidAt, paidAt)
                .SetProperty(p => p.StripePaymentIntentId, paymentIntentId));

        if (updated == 0)
        {
            _logger.LogInformation("Payment {PaymentId} was not an open online payment - paid event ignored", paymentId);
            return false;
        }

        await NotifyAsync(paymentId, (n, details) => n.PaymentConfirmedAsync(details));
        return true;
    }

    public async Task<bool> MarkFailedAsync(int paymentId, string? sessionId = null)
    {
        // Only online attempts fail this way; a waiting counter payment is cancelled by staff (VoidPendingAsync).
        var updated = await _db.Payments
            .Where(p => p.PaymentId == paymentId
                     && p.PaymentStatus == PaymentStatus.Pending
                     && p.PaymentMethod != PaymentMethod.PayAtCounter
                     && (sessionId == null || p.TransactionRef == sessionId))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PaymentStatus, PaymentStatus.Failed));

        return updated > 0;
    }

    public async Task CancelOnlineAttemptAsync(int paymentId, int customerUserId, long? attempt = null)
    {
        var payment = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.PaymentId == paymentId);

        if (payment is null || !await OwnsAsync(payment, customerUserId))
        {
            throw new PaymentException("We couldn't find that payment.");
        }

        if (payment.PaymentStatus != PaymentStatus.Pending || payment.PaymentMethod == PaymentMethod.PayAtCounter)
        {
            return;   // nothing to cancel
        }

        // An old browser tab must not cancel a newer attempt: every attempt has its own CreatedAt.
        if (attempt.HasValue && attempt.Value != payment.CreatedAt.Ticks)
        {
            return;
        }

        // Close the Stripe page first so it can't be paid after we mark it failed.
        if (!string.IsNullOrEmpty(payment.TransactionRef) && !await _stripe.TryExpireSessionAsync(payment.TransactionRef))
        {
            return;   // probably paid already - let the webhook decide
        }

        await MarkFailedAsync(paymentId, payment.TransactionRef);
    }

    // ------------------------------------------------------------------------------------
    // Counter payments (staff)
    // ------------------------------------------------------------------------------------

    public async Task CollectAtCounterAsync(int paymentId, int staffUserId, string method)
    {
        if (!PaymentMethod.CounterMethods.Contains(method))
        {
            throw new PaymentException("Choose cash, card or e-wallet.");
        }

        DateTime? paidAt = DateTime.Now;

        // A waiting counter payment is "pending + pay_at_counter". The real method replaces the placeholder
        // and the staff member who took the money is recorded.
        var updated = await _db.Payments
            .Where(p => p.PaymentId == paymentId
                     && p.PaymentStatus == PaymentStatus.Pending
                     && p.PaymentMethod == PaymentMethod.PayAtCounter)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.PaymentStatus, PaymentStatus.Paid)
                .SetProperty(p => p.PaymentMethod, method)
                .SetProperty(p => p.PaidAt, paidAt)
                .SetProperty(p => p.RecordedByStaffId, (int?)staffUserId));

        if (updated == 0)
        {
            throw new PaymentException("This payment is no longer waiting to be collected.");
        }

        await NotifyAsync(paymentId, (n, details) => n.PaymentConfirmedAsync(details));
    }

    public async Task VoidPendingAsync(int paymentId, int staffUserId)
    {
        var updated = await _db.Payments
            .Where(p => p.PaymentId == paymentId
                     && p.PaymentStatus == PaymentStatus.Pending
                     && p.PaymentMethod == PaymentMethod.PayAtCounter)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.PaymentStatus, PaymentStatus.Failed)
                .SetProperty(p => p.RecordedByStaffId, (int?)staffUserId));

        if (updated == 0)
        {
            throw new PaymentException("This payment is no longer waiting to be collected.");
        }
    }

    // ------------------------------------------------------------------------------------
    // Refunds (admin)
    // ------------------------------------------------------------------------------------

    public async Task RefundAsync(int paymentId, int adminUserId, string? reason = null)
    {
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reason is { Length: > 200 })
        {
            throw new PaymentException("The refund reason can be at most 200 characters.");
        }

        var payment = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.PaymentId == paymentId)
            ?? throw new PaymentException("We couldn't find that payment.");

        if (payment.PaymentStatus != PaymentStatus.Paid)
        {
            throw new PaymentException("Only paid payments can be refunded.");
        }

        // An online payment has a Stripe PaymentIntent id (saved by the webhook). The idempotency key makes a
        // double-click safe: Stripe returns the first refund instead of creating a second one.
        // Counter payments have nothing at Stripe - staff hand the money back and we only record it.
        if (!string.IsNullOrEmpty(payment.StripePaymentIntentId))
        {
            await _stripe.RefundAsync(payment.StripePaymentIntentId, idempotencyKey: $"refund-payment-{payment.PaymentId}");
        }
        else if (!string.IsNullOrEmpty(payment.TransactionRef) && payment.PaymentMethod != PaymentMethod.PayAtCounter
                 && PaymentMethod.IsOnline(payment.PaymentMethod) && payment.RecordedByStaffId is null)
        {
            throw new PaymentException("This online payment has no Stripe charge reference. Refund it in the Stripe Dashboard.");
        }

        // PaidAt is kept (CK_Payments_PaidAt) and RefundedAt is set in the same UPDATE (CK_Payments_RefundedAt).
        DateTime refundedAt = DateTime.Now;
        var updated = await _db.Payments
            .Where(p => p.PaymentId == paymentId && p.PaymentStatus == PaymentStatus.Paid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.PaymentStatus, PaymentStatus.Refunded)
                .SetProperty(p => p.RefundedAt, (DateTime?)refundedAt)
                .SetProperty(p => p.RefundedByStaffId, (int?)adminUserId)
                .SetProperty(p => p.RefundReason, reason));

        if (updated == 0)
        {
            throw new PaymentException("This payment has already been refunded.");
        }

        await NotifyAsync(paymentId, (n, details) => n.PaymentRefundedAsync(details));
    }

    // ------------------------------------------------------------------------------------

    /// <summary>A customer owns a payment if they placed its order.</summary>
    private async Task<bool> OwnsAsync(Payment payment, int customerUserId)
    {
        var order = await _orders.GetOrderAsync(payment.OrderId);
        return order is not null && order.CustomerUserId == customerUserId;
    }

    /// <summary>Tells every notifier. One failing notifier must never undo or hide a payment.</summary>
    private async Task NotifyAsync(int paymentId, Func<IPaymentNotifier, PaymentDetails, Task> send)
    {
        var details = await _queries.GetDetailsAsync(paymentId);
        if (details is null)
        {
            return;
        }

        foreach (var notifier in _notifiers)
        {
            try
            {
                await send(notifier, details);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Notifier} failed for payment {PaymentId}", notifier.GetType().Name, paymentId);
            }
        }
    }
}
