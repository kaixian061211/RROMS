using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Services.Payments;

public class PaymentQueries : IPaymentQueries
{
    private readonly AppDbContext _db;
    private readonly IPaymentOrderProvider _orders;

    public PaymentQueries(
        AppDbContext db,
        IPaymentOrderProvider orders)
    {
        _db = db;
        _orders = orders;
    }

    public async Task<PaymentDetails?> GetDetailsAsync(int paymentId)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

        if (payment is null)
        {
            return null;
        }

        var details = await ToDetailsAsync(
            new List<Payment> { payment });

        return details[0];
    }

    public async Task<PaymentDetails?> GetByOrderAsync(int orderId)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == orderId);

        if (payment is null)
        {
            return null;
        }

        var details = await ToDetailsAsync(
            new List<Payment> { payment });

        return details[0];
    }

    public async Task<Dictionary<int, PaymentDetails>> GetByOrdersAsync(
        IEnumerable<int> orderIds)
    {
        var ids = orderIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<int, PaymentDetails>();
        }

        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p => ids.Contains(p.OrderId))
            .ToListAsync();

        var details = await ToDetailsAsync(payments);

        return details.ToDictionary(d => d.OrderId);
    }

    public async Task<List<PaymentDetails>> GetForCustomerAsync(
        int customerUserId)
    {
        var orders = await _orders.GetOrdersForCustomerAsync(
            customerUserId);

        var orderIds = orders
            .Select(o => o.OrderId)
            .ToList();

        if (orderIds.Count == 0)
        {
            return new List<PaymentDetails>();
        }

        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p => orderIds.Contains(p.OrderId))
            .OrderByDescending(p => p.PaymentId)
            .ToListAsync();

        return await ToDetailsAsync(payments);
    }

    public async Task<List<PaymentDetails>> GetCounterQueueAsync()
    {
        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p =>
                p.PaymentStatus == PaymentStatus.Pending &&
                p.PaymentMethod == PaymentMethod.PayAtCounter)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

        return await ToDetailsAsync(payments);
    }

    public async Task<List<PaymentDetails>> SearchAsync(
        PaymentFilter filter)
    {
        var query = _db.Payments.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(
                p => p.PaymentStatus == filter.Status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Method))
        {
            query = query.Where(
                p => p.PaymentMethod == filter.Method);
        }

        var (rangeStart, rangeEnd) = filter.ResolveRange();

        if (rangeStart.HasValue)
        {
            query = query.Where(
                p => (p.PaidAt ?? p.CreatedAt) >= rangeStart.Value);
        }

        if (rangeEnd.HasValue)
        {
            query = query.Where(
                p => (p.PaidAt ?? p.CreatedAt) < rangeEnd.Value);
        }

        string? text = null;

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();

            if (int.TryParse(search, out var number))
            {
                query = query.Where(
                    p => p.PaymentId == number ||
                         p.OrderId == number);
            }
            else
            {
                text = search;
            }
        }

        var payments = await query
            .OrderByDescending(p => p.PaymentId)
            .ToListAsync();

        var details = await ToDetailsAsync(payments);

        if (text is not null)
        {
            details = details
                .Where(d =>
                    d.CustomerName.Contains(
                        text,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        d.TransactionRef,
                        text,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        d.StripePaymentIntentId,
                        text,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return details;
    }

    private async Task<List<PaymentDetails>> ToDetailsAsync(
        IReadOnlyList<Payment> payments)
    {
        if (payments.Count == 0)
        {
            return new List<PaymentDetails>();
        }

        var orderIds = payments
            .Select(p => p.OrderId)
            .Distinct()
            .ToList();

        var orders = await _orders.GetOrdersAsync(orderIds);

        var staffIds = payments
            .Where(p => p.RecordedByStaffId.HasValue)
            .Select(p => p.RecordedByStaffId!.Value)
            .Distinct()
            .ToList();

        var staffNames = await _orders.GetStaffNamesAsync(staffIds);

        var result = new List<PaymentDetails>(payments.Count);

        foreach (var payment in payments)
        {
            orders.TryGetValue(payment.OrderId, out var order);

            var staffName = "-";

            if (payment.RecordedByStaffId.HasValue &&
                staffNames.TryGetValue(
                    payment.RecordedByStaffId.Value,
                    out var name))
            {
                staffName = name;
            }

            result.Add(new PaymentDetails
            {
                PaymentId = payment.PaymentId,
                OrderId = payment.OrderId,
                OrderStatus = order?.OrderStatus ?? string.Empty,

                CustomerId = order?.CustomerUserId,
                CustomerName = order?.CustomerName ?? "-",
                CustomerEmail = order?.CustomerEmail ?? string.Empty,

                RecordedByStaffId = payment.RecordedByStaffId,
                StaffName = staffName,

                Method = payment.PaymentMethod,
                Status = payment.PaymentStatus,
                Amount = payment.Amount,

                CreatedAt = payment.CreatedAt,
                PaidAt = payment.PaidAt,

                TransactionRef = payment.TransactionRef,
                StripePaymentIntentId = payment.StripePaymentIntentId,

                RefundedAt = payment.RefundedAt,
                RefundedByStaffId = payment.RefundedByStaffId,
                RefundReason = payment.RefundReason
            });
        }

        return result;
    }
}