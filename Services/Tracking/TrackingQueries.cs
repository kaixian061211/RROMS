using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Tracking;

namespace RROMS.Web.Services.Tracking;

public sealed class TrackingQueries
{
    private readonly AppDbContext _db;

    public TrackingQueries(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TrackingSnapshot?> GetAsync(
        int userId,
        int? orderId = null,
        int? reservationId = null,
        int? paymentId = null,
        CancellationToken ct = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));

        var selectedCount = (orderId.HasValue ? 1 : 0)
            + (reservationId.HasValue ? 1 : 0)
            + (paymentId.HasValue ? 1 : 0);

        if (selectedCount != 1
            || orderId is <= 0
            || reservationId is <= 0
            || paymentId is <= 0)
        {
            throw new ArgumentException(
                "Provide exactly one positive order, reservation or payment ID.");
        }

        if (orderId is int selectedOrderId)
        {
            return await (
                from order in _db.Set<Order>().AsNoTracking()
                join payment in _db.Payments.AsNoTracking()
                    on order.OrderId equals payment.OrderId into payments
                from payment in payments.DefaultIfEmpty()
                where order.OrderId == selectedOrderId
                    && order.CustomerUserId == userId
                select new TrackingSnapshot(
                    order.OrderId,
                    order.Status,
                    null,
                    null,
                    (int?)payment.PaymentId,
                    payment.PaymentStatus))
                .SingleOrDefaultAsync(ct);
        }

        if (reservationId is int selectedReservationId)
        {
            return await _db.Set<Reservation>()
                .AsNoTracking()
                .Where(r => r.ReservationId == selectedReservationId
                    && r.CustomerUserId == userId)
                .Select(r => new TrackingSnapshot(
                    null,
                    null,
                    r.ReservationId,
                    r.Status,
                    null,
                    null))
                .SingleOrDefaultAsync(ct);
        }

        var selectedPaymentId = paymentId!.Value;

        return await (
            from payment in _db.Payments.AsNoTracking()
            join order in _db.Set<Order>().AsNoTracking()
                on payment.OrderId equals order.OrderId
            where payment.PaymentId == selectedPaymentId
                && order.CustomerUserId == userId
            select new TrackingSnapshot(
                order.OrderId,
                order.Status,
                null,
                null,
                payment.PaymentId,
                payment.PaymentStatus))
            .SingleOrDefaultAsync(ct);
    }
}