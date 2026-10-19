using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments.Notifications;

namespace RROMS.Web.Services.Payments;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IPaymentOrderProvider _orders;
    private readonly IPaymentQueries _queries;
    private readonly IEnumerable<IPaymentNotifier> _notifiers;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        AppDbContext db,
        IPaymentOrderProvider orders,
        IPaymentQueries queries,
        IEnumerable<IPaymentNotifier> notifiers,
        IHttpContextAccessor http,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _orders = orders;
        _queries = queries;
        _notifiers = notifiers;
        _http = http;
        _logger = logger;
    }

    public async Task<Payment> CreateCounterPaymentAsync(int orderId)
    {
        RequireStaff();

        var order = await GetOrderAsync(orderId);

        var existing = await _db.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.OrderId == orderId);

        if (existing is not null)
        {
            if (existing.PaymentStatus is
                PaymentStatus.Paid or PaymentStatus.Refunded)
            {
                throw new PaymentException(
                    "This order has already been paid or refunded.");
            }

            if (!IsCounter(existing))
            {
                throw new PaymentException(
                    "Resolve the online payment before starting a counter payment.");
            }

            if (existing.PaymentStatus == PaymentStatus.Pending)
            {
                if (existing.Amount != order.TotalAmount)
                {
                    throw new PaymentException(
                        "The order total changed. Cancel the waiting payment and create it again.");
                }

                return existing;
            }

            if (existing.PaymentStatus != PaymentStatus.Failed)
            {
                throw new PaymentException(
                    "This order has already been paid or refunded.");
            }

            var reopened = await _db.Payments
                .Where(p =>
                    p.PaymentId == existing.PaymentId &&
                    p.PaymentStatus == PaymentStatus.Failed &&
                    p.PaymentMethod == PaymentMethod.PayAtCounter &&
                    p.Amount == existing.Amount &&
                    p.RecordedByStaffId == null &&
                    p.TransactionRef == null &&
                    p.StripePaymentIntentId == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(
                        p => p.PaymentStatus,
                        PaymentStatus.Pending)
                    .SetProperty(
                        p => p.Amount,
                        order.TotalAmount)
                    .SetProperty(
                        p => p.RecordedByStaffId,
                        (int?)null));

            if (reopened == 0)
            {
                throw new PaymentException(
                    "The payment changed. Refresh and try again.");
            }

            return await GetPaymentAsync(existing.PaymentId);
        }

        var payment = new Payment
        {
            OrderId = order.OrderId,
            Amount = order.TotalAmount,
            PaymentMethod = PaymentMethod.PayAtCounter,
            PaymentStatus = PaymentStatus.Pending,
            CreatedAt = DateTime.Now
        };

        _db.Payments.Add(payment);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _db.Entry(payment).State = EntityState.Detached;

            _logger.LogError(
                ex,
                "Could not save payment for order {OrderId}",
                orderId);

            throw new PaymentException(
                "Unable to save the payment. Refresh the order and try again.");
        }

        return payment;
    }

    public async Task<decimal> CollectAtCounterAsync(
        int paymentId,
        string method,
        decimal? cashReceived = null)
    {
        var staffId = RequireStaff();

        if (!PaymentMethod.CounterMethods.Contains(method))
        {
            throw new PaymentException(
                "Choose cash, card or e-wallet.");
        }

        var payment = await GetPaymentAsync(paymentId);

        if (!IsCounter(payment) ||
            payment.PaymentStatus != PaymentStatus.Pending)
        {
            throw new PaymentException(
                "This payment is not waiting at the counter.");
        }

        var order = await GetOrderAsync(payment.OrderId);

        if (order.TotalAmount != payment.Amount)
        {
            throw new PaymentException(
                "The order total changed. Cancel the waiting payment and create it again.");
        }

        decimal change = 0;

        if (method == PaymentMethod.Cash)
        {
            if (!cashReceived.HasValue)
            {
                throw new PaymentException(
                    "Enter the cash received.");
            }

            ValidateAmount(cashReceived.Value);

            if (cashReceived.Value < payment.Amount)
            {
                throw new PaymentException(
                    "Cash received is less than the amount due.");
            }

            change = cashReceived.Value - payment.Amount;
        }

        var updated = await _db.Payments
            .Where(p =>
                p.PaymentId == paymentId &&
                p.PaymentStatus == PaymentStatus.Pending &&
                p.PaymentMethod == PaymentMethod.PayAtCounter &&
                p.Amount == payment.Amount &&
                p.RecordedByStaffId == null &&
                p.TransactionRef == null &&
                p.StripePaymentIntentId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(
                    p => p.PaymentStatus,
                    PaymentStatus.Paid)
                .SetProperty(
                    p => p.PaymentMethod,
                    method)
                .SetProperty(
                    p => p.PaidAt,
                    (DateTime?)DateTime.Now)
                .SetProperty(
                    p => p.RecordedByStaffId,
                    (int?)staffId));

        if (updated == 0)
        {
            throw new PaymentException(
                "The payment changed or was already collected. Refresh the page.");
        }

        await NotifyAsync(paymentId, refunded: false);

        return change;
    }

    public async Task VoidPendingAsync(int paymentId)
    {
        var staffId = RequireStaff();

        var updated = await _db.Payments
            .Where(p =>
                p.PaymentId == paymentId &&
                p.PaymentStatus == PaymentStatus.Pending &&
                p.PaymentMethod == PaymentMethod.PayAtCounter &&
                p.RecordedByStaffId == null &&
                p.TransactionRef == null &&
                p.StripePaymentIntentId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(
                    p => p.PaymentStatus,
                    PaymentStatus.Failed));

        if (updated == 0)
        {
            throw new PaymentException(
                "This payment is no longer waiting at the counter.");
        }

        _logger.LogInformation(
            "Staff {StaffId} cancelled counter payment {PaymentId}",
            staffId,
            paymentId);
    }

    public async Task RefundCounterPaymentAsync(
        int paymentId,
        string reason)
    {
        var adminId = RequireStaff(adminOnly: true);

        reason = reason?.Trim() ?? string.Empty;

        if (reason.Length == 0 || reason.Length > 200)
        {
            throw new PaymentException(
                "Enter a refund reason between 1 and 200 characters.");
        }

        // Staff return the money through the original counter/terminal
        // process before recording this refund.
        var updated = await _db.Payments
            .Where(p =>
                p.PaymentId == paymentId &&
                p.PaymentStatus == PaymentStatus.Paid &&
                p.PaymentMethod != PaymentMethod.PayAtCounter &&
                p.RecordedByStaffId != null &&
                p.TransactionRef == null &&
                p.StripePaymentIntentId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(
                    p => p.PaymentStatus,
                    PaymentStatus.Refunded)
                .SetProperty(
                    p => p.RefundedAt,
                    (DateTime?)DateTime.Now)
                .SetProperty(
                    p => p.RefundedByStaffId,
                    (int?)adminId)
                .SetProperty(
                    p => p.RefundReason,
                    reason));

        if (updated == 0)
        {
            throw new PaymentException(
                "Only a paid counter payment can be refunded here.");
        }

        await NotifyAsync(paymentId, refunded: true);
    }

    private int RequireStaff(bool adminOnly = false)
    {
        var user = _http.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new PaymentException(
                "Sign in before recording a payment.");
        }

        var allowed =
            user.IsInRole(PaymentRoles.Admin) ||
            (!adminOnly && user.IsInRole(PaymentRoles.Staff));

        if (!allowed)
        {
            throw new PaymentException(
                "You do not have permission to perform this action.");
        }

        if (!user.TryGetPaymentUserId(out var id) || id <= 0)
        {
            throw new PaymentException(
                "The signed-in user has no valid staff ID.");
        }

        return id;
    }

    private async Task<PaymentOrderInfo> GetOrderAsync(int orderId)
    {
        if (orderId <= 0)
        {
            throw new PaymentException(
                "Choose a valid order.");
        }

        var order = await _orders.GetOrderAsync(orderId);

        if (order is null || order.OrderId != orderId)
        {
            throw new PaymentException(
                "The order could not be found.");
        }

        ValidateAmount(order.TotalAmount);

        return order;
    }

    private async Task<Payment> GetPaymentAsync(int paymentId)
    {
        return await _db.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.PaymentId == paymentId)
            ?? throw new PaymentException(
                "The payment could not be found.");
    }

    private static bool IsCounter(Payment payment)
    {
        return payment.PaymentMethod == PaymentMethod.PayAtCounter
            && payment.RecordedByStaffId is null
            && payment.TransactionRef is null
            && payment.StripePaymentIntentId is null;
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0 ||
            amount > 99999999.99m ||
            decimal.Round(amount, 2) != amount)
        {
            throw new PaymentException(
                "The amount must be positive and have no more than two decimal places.");
        }
    }

    private async Task NotifyAsync(int paymentId, bool refunded)
    {
        try
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
                    if (refunded)
                    {
                        await notifier.PaymentRefundedAsync(details);
                    }
                    else
                    {
                        await notifier.PaymentConfirmedAsync(details);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Notification failed for payment {PaymentId}",
                        paymentId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not load notification details for payment {PaymentId}",
                paymentId);
        }
    }
}