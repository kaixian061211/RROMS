using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Services.Payments.Gateways;
using RROMS.Web.Services.Payments.Notifications;
using RROMS.Web.Services.Notifications;

namespace RROMS.Web.Services.Payments;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IStripeGateway _stripe;
    private readonly IPaymentOrderProvider _orders;
    private readonly IPaymentQueries _queries;
    private readonly IEnumerable<IPaymentNotifier> _notifiers;
    private readonly ILogger<PaymentService> _logger;
    private readonly StripeSettings _stripeSettings;
    private readonly INotificationService _notifications;
    private readonly INotificationDelivery _notificationDelivery;

    public PaymentService(
        AppDbContext db,
        INotificationService notifications,
        INotificationDelivery notificationDelivery,
        IStripeGateway stripe,
        IPaymentOrderProvider orders,
        IPaymentQueries queries,
        IEnumerable<IPaymentNotifier> notifiers,
        ILogger<PaymentService> logger,
        IOptions<StripeSettings> stripeSettings)
    {
        _db = db;
        _notifications = notifications;
        _notificationDelivery = notificationDelivery;
        _stripe = stripe;
        _orders = orders;
        _queries = queries;
        _notifiers = notifiers;
        _logger = logger;
        _stripeSettings = stripeSettings.Value;
    }

    // Customer checkout

    public async Task<PaymentResult> StartCheckoutAsync(
        int orderId,
        int customerUserId,
        string method,
        string baseUrl)
    {

        if (orderId <= 0 || customerUserId <= 0)
        {
            throw new PaymentException(
                "We couldn't find that order.");
        }

        if (!PaymentMethod.CheckoutChoices.Contains(method))
        {
            throw new PaymentException(
                "Please choose how you would like to pay.");
        }

        PaymentOrderInfo? order;
        Payment payment;

        // Expire Stripe sessions outside the transaction, then revalidate this
        // exact attempt under the order lock before reopening it.
        var checkoutOrder = await _orders.GetOrderAsync(orderId);
        if (checkoutOrder is null || checkoutOrder.CustomerUserId != customerUserId)
            throw new PaymentException("We couldn't find that order.");
        if (checkoutOrder.OrderStatus == OrderStatus.Cancelled)
            throw new PaymentException("Cancelled orders cannot be paid.");

        var previous = await _db.Payments.AsNoTracking()
            .SingleOrDefaultAsync(p => p.OrderId == orderId);
        if (previous is { PaymentStatus: PaymentStatus.Pending }
            && previous.PaymentMethod != PaymentMethod.PayAtCounter
            && !string.IsNullOrEmpty(previous.TransactionRef)
            && !await _stripe.TryExpireSessionAsync(previous.TransactionRef))
        {
            throw new PaymentException("Your previous payment is still being processed. Please wait a moment and refresh.");
        }

        await using (var transaction = await _db.Database.BeginTransactionAsync())
        {
            await LockOrderForPaymentAsync(orderId, customerUserId);
            order = await _orders.GetOrderAsync(orderId);

            if (order is null ||
                order.CustomerUserId != customerUserId)
            {
                throw new PaymentException(
                    "We couldn't find that order.");
            }

            if (order.OrderStatus == OrderStatus.Cancelled)
            {
                throw new PaymentException(
                    "Cancelled orders cannot be paid.");
            }

            if (order.TotalAmount <= 0 ||
                order.TotalAmount > 99999999.99m ||
                decimal.Round(order.TotalAmount, 2) != order.TotalAmount)
            {
                throw new PaymentException(
                    "This order does not have a valid payment amount.");
            }

            var existing = await _db.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.OrderId == orderId);

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
                    _db.Entry(payment).State = EntityState.Detached;

                    _logger.LogWarning(
                        ex,
                        "Could not create a payment for order {OrderId}",
                        orderId);

                    throw new PaymentException(
                        "We couldn't save the payment. Please check its status before trying again.");
                }
            }
            else
            {
                if (previous is null || existing.CreatedAt != previous.CreatedAt
                    || existing.TransactionRef != previous.TransactionRef
                    || existing.PaymentStatus != previous.PaymentStatus)
                {
                    throw new PaymentException("The payment changed while checkout was starting. Please refresh before trying again.");
                }
                payment = await ReopenAsync(
                    existing,
                    order,
                    method);
            }

            await transaction.CommitAsync();
        }

        if (method == PaymentMethod.PayAtCounter)
        {
            return new PaymentResult
            {
                Payment = payment
            };
        }

        StripeSessionInfo session;

        try
        {
            session = await _stripe.CreateCheckoutSessionAsync(
                payment,
                order.CustomerEmail,
                successUrl:
                    $"{baseUrl}/Payments/Success?paymentId={payment.PaymentId}",
                cancelUrl:
                    $"{baseUrl}/Payments/Cancel?paymentId={payment.PaymentId}&attempt={payment.CreatedAt.Ticks}");
        }
        catch (PaymentException ex)
            when (!ex.PaymentMayBeInProgress)
        {
            await _db.Payments
                .Where(p =>
                    p.PaymentId == payment.PaymentId &&
                    p.CreatedAt == payment.CreatedAt &&
                    p.PaymentStatus == PaymentStatus.Pending &&
                    p.PaymentMethod == payment.PaymentMethod &&
                    p.TransactionRef == null &&
                    p.RecordedByStaffId == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(
                        p => p.PaymentStatus,
                        PaymentStatus.Failed));

            throw;
        }

        // A webhook may already have marked this attempt paid.
        // Save references without overwriting a newer attempt.
        var saved = await _db.Payments
            .Where(p =>
                p.PaymentId == payment.PaymentId &&
                p.OrderId == payment.OrderId &&
                p.CreatedAt == payment.CreatedAt &&
                p.Amount == payment.Amount &&
                p.PaymentMethod == payment.PaymentMethod &&
                p.RecordedByStaffId == null &&
                (p.TransactionRef == null ||
                 p.TransactionRef == session.SessionId) &&
                (session.PaymentIntentId == null ||
                 p.StripePaymentIntentId == null ||
                 p.StripePaymentIntentId == session.PaymentIntentId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(
                    p => p.TransactionRef,
                    session.SessionId)
                .SetProperty(
                    p => p.StripePaymentIntentId,
                    p => p.StripePaymentIntentId ??
                         session.PaymentIntentId));

        if (saved == 0)
        {
            _logger.LogWarning(
                "Checkout result did not match payment {PaymentId}'s current attempt",
                payment.PaymentId);

            throw new PaymentException(
                "The payment changed while checkout was starting. Please refresh before trying again.");
        }

        return new PaymentResult
        {
            Payment = payment,
            RedirectUrl = session.Url
        };
    }

    // A same-value SQL Server update advances RowVersion and holds an exclusive
    // lock until commit. Cancellation already rejects a stale order RowVersion.
    private async Task LockOrderForPaymentAsync(int orderId, int customerUserId)
    {
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("An order lock requires a transaction.");

        var updated = await _db.Set<Order>()
            .Where(o => o.OrderId == orderId
                && o.CustomerUserId == customerUserId
                && o.Status != OrderStatus.Cancelled)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, o => o.Status));

        if (updated == 0)
            throw new PaymentException("The order was not found or has been cancelled. Please refresh before paying.");
    }

    private async Task<Payment> ReopenAsync(
        Payment existing,
        PaymentOrderInfo order,
        string method)
    {
        if (existing.PaymentStatus == PaymentStatus.Paid)
        {
            throw new PaymentException(
                "This order has already been paid.");
        }

        if (existing.PaymentStatus == PaymentStatus.Refunded)
        {
            throw new PaymentException(
                "This order's payment was refunded, so it can't be paid again here. Please speak to the restaurant.");
        }

        if (existing.PaymentStatus == PaymentStatus.Pending)
        {
            if (PaymentMethod.IsOnline(existing.PaymentMethod) &&
                string.IsNullOrWhiteSpace(existing.TransactionRef))
            {
                throw new PaymentException(
                    "The previous online payment is not confirmed yet. Please contact the restaurant to check it before paying again.",
                    paymentMayBeInProgress: true);
            }

            await _db.Payments
                .Where(p =>
                    p.PaymentId == existing.PaymentId &&
                    p.CreatedAt == existing.CreatedAt &&
                    p.PaymentStatus == PaymentStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(
                        p => p.PaymentStatus,
                        PaymentStatus.Failed));
        }

        DateTime now = DateTime.Now;

        if (now <= existing.CreatedAt)
        {
            now = existing.CreatedAt.AddTicks(1);
        }

        var claimed = await _db.Payments
            .Where(p =>
                p.PaymentId == existing.PaymentId &&
                p.CreatedAt == existing.CreatedAt &&
                p.PaymentStatus == PaymentStatus.Failed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(
                    p => p.PaymentStatus,
                    PaymentStatus.Pending)
                .SetProperty(
                    p => p.PaymentMethod,
                    method)
                .SetProperty(
                    p => p.Amount,
                    order.TotalAmount)
                .SetProperty(
                    p => p.CreatedAt,
                    now)
                .SetProperty(
                    p => p.PaidAt,
                    (DateTime?)null)
                .SetProperty(
                    p => p.RecordedByStaffId,
                    (int?)null)
                .SetProperty(
                    p => p.TransactionRef,
                    (string?)null)
                .SetProperty(
                    p => p.StripePaymentIntentId,
                    (string?)null));

        if (claimed == 0)
        {
            throw new PaymentException(
                "A payment for this order is already in progress.");
        }

        return await _db.Payments
            .AsNoTracking()
            .FirstAsync(p => p.PaymentId == existing.PaymentId);
    }

    // Stripe results

    public async Task<bool> MarkPaidOnlineAsync(
        OnlinePaymentConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);

        bool Reject(string reason)
        {
            _logger.LogWarning(
                "Rejected Stripe confirmation for payment {PaymentId}: {Reason}",
                confirmation.PaymentId,
                reason);

            return false;
        }

        if (confirmation.PaymentId <= 0 ||
            confirmation.OrderId <= 0 ||
            confirmation.AmountTotal <= 0 ||
            string.IsNullOrWhiteSpace(confirmation.SessionId) ||
            confirmation.SessionId.Length > 100 ||
            string.IsNullOrWhiteSpace(confirmation.Currency) ||
            confirmation.PaymentIntentId is { Length: > 100 })
        {
            return Reject(
                "Missing or invalid payment references.");
        }

        var payment = await _db.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.PaymentId == confirmation.PaymentId);

        if (payment is null ||
            payment.OrderId != confirmation.OrderId ||
            !PaymentMethod.IsOnline(payment.PaymentMethod) ||
            payment.RecordedByStaffId is not null)
        {
            return Reject(
                "The order or payment method does not match an online payment.");
        }

        var expectedAmount = (long)Math.Round(
            payment.Amount * 100m,
            MidpointRounding.AwayFromZero);

        if (confirmation.AmountTotal != expectedAmount ||
            string.IsNullOrWhiteSpace(_stripeSettings.Currency) ||
            !string.Equals(
                confirmation.Currency.Trim(),
                _stripeSettings.Currency.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return Reject(
                "The amount or currency does not match.");
        }

        if (confirmation.AttemptTicks is long attemptTicks &&
            attemptTicks != payment.CreatedAt.Ticks)
        {
            return Reject(
                "The confirmation belongs to another checkout attempt.");
        }

        // Older sessions without attempt metadata need a stored matching ID.
        if ((payment.TransactionRef is not null &&
             payment.TransactionRef != confirmation.SessionId) ||
            (payment.TransactionRef is null &&
             confirmation.AttemptTicks is null))
        {
            return Reject(
                "The Checkout Session does not match the current attempt.");
        }

        var paymentIntentId =
            string.IsNullOrWhiteSpace(confirmation.PaymentIntentId)
                ? null
                : confirmation.PaymentIntentId;

        if (payment.StripePaymentIntentId is not null &&
            paymentIntentId is not null &&
            payment.StripePaymentIntentId != paymentIntentId)
        {
            return Reject(
                "The PaymentIntent does not match.");
        }

        // Repeat important checks in the UPDATE for concurrent changes.
        var matchingAttempt = _db.Payments.Where(p =>
            p.PaymentId == payment.PaymentId &&
            p.OrderId == payment.OrderId &&
            p.CreatedAt == payment.CreatedAt &&
            p.Amount == payment.Amount &&
            p.PaymentMethod == payment.PaymentMethod &&
            p.RecordedByStaffId == null &&
            (p.TransactionRef == null ||
             p.TransactionRef == confirmation.SessionId) &&
            (paymentIntentId == null ||
             p.StripePaymentIntentId == null ||
             p.StripePaymentIntentId == paymentIntentId));

        DateTime? paidAt = DateTime.Now;

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var updated = await matchingAttempt
            .Where(p => p.PaymentStatus == PaymentStatus.Pending
                     || p.PaymentStatus == PaymentStatus.Failed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.PaymentStatus, PaymentStatus.Paid)
                .SetProperty(p => p.PaidAt, paidAt)
                .SetProperty(p => p.TransactionRef, confirmation.SessionId)
                .SetProperty(
                    p => p.StripePaymentIntentId,
                    p => p.StripePaymentIntentId ?? paymentIntentId));

        if (updated == 0)
        {
            // A repeated callback may supply a late payment reference.
            // Preserve the status and avoid another notification.
            await matchingAttempt
                .Where(p => p.PaymentStatus == PaymentStatus.Paid
                         || p.PaymentStatus == PaymentStatus.Refunded)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(
                        p => p.TransactionRef,
                        confirmation.SessionId)
                    .SetProperty(
                        p => p.StripePaymentIntentId,
                        p => p.StripePaymentIntentId ?? paymentIntentId));

            await transaction.CommitAsync();
            return false;
        }

        var notificationId = await SavePaymentNotificationAsync(
            payment.PaymentId,
            isRefund: false);

        await transaction.CommitAsync();

        await _notificationDelivery.DeliverAsync(notificationId);

        await NotifyAsync(
            payment.PaymentId,
            (n, details) => n.PaymentConfirmedAsync(details));

        return true;
    }

    public async Task<bool> MarkFailedAsync(
        int paymentId,
        string? sessionId = null)
    {
        var updated = await _db.Payments
            .Where(p =>
                p.PaymentId == paymentId &&
                p.PaymentStatus == PaymentStatus.Pending &&
                p.PaymentMethod != PaymentMethod.PayAtCounter &&
                (sessionId == null ||
                 p.TransactionRef == sessionId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(
                    p => p.PaymentStatus,
                    PaymentStatus.Failed));

        return updated > 0;
    }

    public async Task CancelOnlineAttemptAsync(
        int paymentId,
        int customerUserId,
        long? attempt = null)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

        if (payment is null ||
            !await OwnsAsync(payment, customerUserId))
        {
            throw new PaymentException(
                "We couldn't find that payment.");
        }

        if (payment.PaymentStatus != PaymentStatus.Pending ||
            payment.PaymentMethod == PaymentMethod.PayAtCounter)
        {
            throw new PaymentException(
                "This payment is not waiting for online cancellation. Open your order to check its current status.");
        }

        if (attempt is not long attemptTicks ||
            attemptTicks != payment.CreatedAt.Ticks)
        {
            throw new PaymentException(
                "This checkout link is no longer current. Open your order to check its payment status.");
        }

        if (string.IsNullOrWhiteSpace(payment.TransactionRef))
        {
            throw new PaymentException(
                "This payment is not confirmed yet. Please contact the restaurant to check it before cancelling or paying again.",
                paymentMayBeInProgress: true);
        }

        if (!await _stripe.TryExpireSessionAsync(
                payment.TransactionRef))
        {
            throw new PaymentException(
                "Cancellation is not confirmed. Your payment may still be processing. Please check your order.",
                paymentMayBeInProgress: true);
        }

        var cancelled = await _db.Payments
            .Where(p =>
                p.PaymentId == paymentId &&
                p.CreatedAt == payment.CreatedAt &&
                p.PaymentStatus == PaymentStatus.Pending &&
                p.TransactionRef == payment.TransactionRef)
            .ExecuteUpdateAsync(s => s
                .SetProperty(
                    p => p.PaymentStatus,
                    PaymentStatus.Failed));

        if (cancelled == 0)
        {
            throw new PaymentException(
                "This payment changed while cancellation was being checked. Please open your order to check its status.");
        }
    }

    // Counter payments

    public async Task CollectAtCounterAsync(
        int paymentId,
        int staffUserId,
        string method)
    {
        if (paymentId <= 0 || staffUserId <= 0)
        {
            throw new PaymentException(
                "The payment or staff ID is invalid.");
        }

        if (!PaymentMethod.CounterMethods.Contains(method))
        {
            throw new PaymentException("Choose cash, card or e-wallet.");
        }

        var payment = await _db.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(p =>
                p.PaymentId == paymentId &&
                p.PaymentStatus == PaymentStatus.Pending &&
                p.PaymentMethod == PaymentMethod.PayAtCounter)
            ?? throw new PaymentException(
                "This payment is no longer waiting to be collected.");

        var order = await _orders.GetOrderAsync(payment.OrderId)
            ?? throw new PaymentException(
                "We couldn't find the order for this payment.");

        if (order.OrderStatus == OrderStatus.Cancelled)
        {
            throw new PaymentException(
                "Cancelled orders cannot be paid.");
        }

        if (payment.Amount <= 0 ||
            payment.Amount > 99999999.99m ||
            decimal.Round(payment.Amount, 2) != payment.Amount)
        {
            throw new PaymentException(
                "This payment has an invalid amount.");
        }

        DateTime? paidAt = DateTime.Now;

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var updated = await _db.Payments
            .Where(p => p.PaymentId == paymentId
                     && p.PaymentStatus == PaymentStatus.Pending
                     && p.PaymentMethod == PaymentMethod.PayAtCounter
                     && p.Amount == payment.Amount
                     && p.OrderId == payment.OrderId
                     && p.CreatedAt == payment.CreatedAt)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.PaymentStatus, PaymentStatus.Paid)
                .SetProperty(p => p.PaymentMethod, method)
                .SetProperty(p => p.PaidAt, paidAt)
                .SetProperty(p => p.RecordedByStaffId, (int?)staffUserId));

        if (updated == 0)
        {
            throw new PaymentException(
                "This payment changed while it was being collected. Check its current status before trying again.");
        }

        var notificationId = await SavePaymentNotificationAsync(
            paymentId,
            isRefund: false);

        await transaction.CommitAsync();

        await _notificationDelivery.DeliverAsync(notificationId);

        await NotifyAsync(
            paymentId,
            (n, details) => n.PaymentConfirmedAsync(details));
    }

    public async Task VoidPendingAsync(
        int paymentId,
        int staffUserId)
    {
        if (paymentId <= 0 || staffUserId <= 0)
        {
            throw new PaymentException(
                "The payment or staff ID is invalid.");
        }

        var updated = await _db.Payments
            .Where(p =>
                p.PaymentId == paymentId &&
                p.PaymentStatus == PaymentStatus.Pending &&
                p.PaymentMethod == PaymentMethod.PayAtCounter)
            .ExecuteUpdateAsync(s => s
                .SetProperty(
                    p => p.PaymentStatus,
                    PaymentStatus.Failed)
                .SetProperty(
                    p => p.RecordedByStaffId,
                    (int?)staffUserId));

        if (updated == 0)
        {
            throw new PaymentException(
                "This payment is no longer waiting to be collected.");
        }
    }

    // Refunds

    public Task RefundAsync(
        int paymentId,
        int staffUserId,
        string? reason = null) =>
        RefundCoreAsync(
            paymentId,
            staffUserId,
            reason,
            recheckOnly: false);

    public Task RecheckRefundAsync(
        int paymentId,
        int staffUserId,
        string? reason = null) =>
        RefundCoreAsync(
            paymentId,
            staffUserId,
            reason,
            recheckOnly: true);

    private async Task RefundCoreAsync(
        int paymentId,
        int staffUserId,
        string? reason,
        bool recheckOnly)
    {
        if (paymentId <= 0 || staffUserId <= 0)
        {
            throw new PaymentException(
                "The payment or staff ID is invalid.");
        }

        reason = string.IsNullOrWhiteSpace(reason)
            ? null
            : reason.Trim();

        if (reason is { Length: > 200 })
        {
            throw new PaymentException(
                "The refund reason can be at most 200 characters.");
        }

        var payment = await _db.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.PaymentId == paymentId)
            ?? throw new PaymentException(
                "We couldn't find that payment.");

        if (payment.PaymentStatus != PaymentStatus.Paid)
        {
            throw new PaymentException(
                "Only paid payments can be refunded.");
        }

        if (payment.Amount <= 0 ||
            payment.Amount > 99999999.99m ||
            decimal.Round(payment.Amount, 2) != payment.Amount)
        {
            throw new PaymentException(
                "This payment has an invalid refund amount.");
        }

        bool onlinePayment =
            payment.RecordedByStaffId is null &&
            PaymentMethod.IsOnline(payment.PaymentMethod);

        if (recheckOnly && !onlinePayment)
        {
            throw new PaymentException(
                "Only online payments have a Stripe refund to recheck.");
        }

        if (onlinePayment)
        {
            if (string.IsNullOrWhiteSpace(
                    payment.StripePaymentIntentId))
            {
                throw new PaymentException(
                    "This online payment has no Stripe PaymentIntent reference. Check it in Stripe before refunding.");
            }

            if (recheckOnly)
            {
                await _stripe.RecheckRefundAsync(
                    payment.StripePaymentIntentId,
                    payment.Amount);
            }
            else
            {
                await _stripe.RefundAsync(
                    payment.StripePaymentIntentId,
                    payment.Amount,
                    $"refund-payment-{payment.PaymentId}");
            }
        }
        else if (
            payment.RecordedByStaffId is not > 0 ||
            !PaymentMethod.CounterMethods.Contains(
                payment.PaymentMethod) ||
            !string.IsNullOrWhiteSpace(
                payment.StripePaymentIntentId) ||
            !string.IsNullOrWhiteSpace(
                payment.TransactionRef))
        {
            throw new PaymentException(
                "The payment references do not match an online or staff-recorded counter payment. Check the record before refunding.");
        }

        // Counter refunds record money already returned manually.
        DateTime refundedAt = DateTime.Now;

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var updated = await _db.Payments
            .Where(p =>
                p.PaymentId == paymentId &&
                p.PaymentStatus == PaymentStatus.Paid &&
                p.CreatedAt == payment.CreatedAt &&
                p.OrderId == payment.OrderId &&
                p.Amount == payment.Amount &&
                p.PaymentMethod == payment.PaymentMethod &&
                p.RecordedByStaffId == payment.RecordedByStaffId &&
                p.TransactionRef == payment.TransactionRef &&
                p.StripePaymentIntentId ==
                    payment.StripePaymentIntentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(
                    p => p.PaymentStatus,
                    PaymentStatus.Refunded)
                .SetProperty(
                    p => p.RefundedAt,
                    (DateTime?)refundedAt)
                .SetProperty(
                    p => p.RefundedByStaffId,
                    (int?)staffUserId)
                .SetProperty(
                    p => p.RefundReason,
                    reason));

        if (updated == 0)
        {
            throw new PaymentException(
                "The payment changed while the refund was being processed. Check its current status and Stripe before trying again.");
        }

        var notificationId = await SavePaymentNotificationAsync(
            paymentId,
            isRefund: true);

        await transaction.CommitAsync();

        await _notificationDelivery.DeliverAsync(notificationId);

        await NotifyAsync(
            paymentId,
            (notifier, details) => notifier.PaymentRefundedAsync(details));
    }

    private async Task<bool> OwnsAsync(
        Payment payment,
        int customerUserId)
    {
        var order = await _orders.GetOrderAsync(payment.OrderId);

        return order is not null &&
               order.CustomerUserId == customerUserId;
    }

    private async Task NotifyAsync(
        int paymentId,
        Func<IPaymentNotifier, PaymentDetails, Task> send)
    {
        PaymentDetails? details;

        try
        {
            details = await _queries.GetDetailsAsync(paymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not load notification details for payment {PaymentId}",
                paymentId);

            return;
        }

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
                _logger.LogError(
                    ex,
                    "{Notifier} failed for payment {PaymentId}",
                    notifier.GetType().Name,
                    paymentId);
            }
        }
    }

    private async Task<int> SavePaymentNotificationAsync(
        int paymentId,
        bool isRefund)
    {
        var details = await (
            from payment in _db.Payments.AsNoTracking()
            join order in _db.Set<Order>().AsNoTracking()
                on payment.OrderId equals order.OrderId
            where payment.PaymentId == paymentId
            select new
            {
                payment.PaymentId,
                payment.OrderId,
                payment.Amount,
                order.CustomerUserId
            }).SingleAsync();

        var amount = PaymentFormatter.Money(details.Amount);

        var notification = await _notifications.StageAsync(new NotificationInput
        {
            UserId = details.CustomerUserId,
            Type = NotificationTypes.Payment,
            Title = isRefund
                ? "Payment refunded"
                : "Payment confirmed",
            Message = isRefund
                ? $"Your payment of {amount} for order #{details.OrderId} has been refunded."
                : $"We have received your payment of {amount} for order #{details.OrderId}.",
            PaymentId = details.PaymentId
        });

        // Save within the transaction started by the payment action.
        await _db.SaveChangesAsync();

        return notification.NotificationId;
    }
}
