using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Services.Reports;

// Read-only database projections and lookup loading for ReportService.
public partial class ReportService
{
    private sealed record PaymentData(
        int PaymentId,
        int OrderId,
        int? CustomerUserId,
        string Method,
        string Status,
        decimal Amount,
        DateTime? PaidAt,
        DateTime? RefundedAt,
        string? Reference,
        string? RefundReason);

    private sealed record OrderData(
        int OrderId,
        int CustomerUserId,
        int TableId,
        string Status,
        DateTime CreatedAt,
        int ItemCount,
        decimal Total,
        string? PaymentStatus);

    private sealed record ReservationData(
        int ReservationId,
        int CustomerUserId,
        int TableId,
        DateOnly Date,
        TimeOnly Slot,
        int PartySize,
        string Status,
        DateTime CreatedAt,
        string? SpecialRequest);

    private sealed record SoldItemData(
        int MenuItemId,
        int Quantity,
        decimal Amount);

    /// <summary>
    /// Loads collections and refunds using independent date filters.
    /// Missing orders do not remove money from the sales report.
    /// </summary>
    private async Task<List<PaymentData>> LoadPaymentsAsync(
        ReportDateRange range,
        CancellationToken ct)
    {
        var start = range.FromStored;
        var end = range.ToExclusiveStored;

        return await (
            from p in _db.Payments.AsNoTracking()
            where (
                (p.PaymentStatus == PaymentStatus.Paid ||
                 p.PaymentStatus == PaymentStatus.Refunded)
                && p.PaidAt >= start
                && p.PaidAt < end)
                || (
                    p.PaymentStatus == PaymentStatus.Refunded
                    && p.RefundedAt >= start
                    && p.RefundedAt < end)
            join o in _db.Set<Order>()
                on p.OrderId equals o.OrderId into orders
            from o in orders.DefaultIfEmpty()
            select new PaymentData(
                p.PaymentId,
                p.OrderId,
                o == null ? (int?)null : o.CustomerUserId,
                p.PaymentMethod,
                p.PaymentStatus,
                p.Amount,
                p.PaidAt,
                p.RefundedAt,
                p.TransactionRef,
                p.RefundReason))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Loads orders created in the period.
    /// Saved item totals are calculated in SQL.
    /// </summary>
    private async Task<List<OrderData>> LoadOrdersAsync(
        ReportDateRange range,
        CancellationToken ct)
    {
        var start = range.FromStored;
        var end = range.ToExclusiveStored;

        return await (
            from o in _db.Set<Order>().AsNoTracking()
            where o.CreatedAt >= start && o.CreatedAt < end
            join p in _db.Payments
                on o.OrderId equals p.OrderId into payments
            from p in payments.DefaultIfEmpty()
            orderby o.CreatedAt, o.OrderId
            select new OrderData(
                o.OrderId,
                o.CustomerUserId,
                o.TableId,
                o.Status,
                o.CreatedAt,
                o.Items.Sum(i => i.Quantity),
                o.Items.Sum(i => i.Quantity * i.UnitPrice),
                p == null ? null : p.PaymentStatus))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Loads reservations scheduled in the period.
    /// ReservationDate is already restaurant-local.
    /// </summary>
    private async Task<List<ReservationData>> LoadReservationsAsync(
        ReportDateRange range,
        CancellationToken ct)
    {
        var first = range.From;
        var last = range.ToExclusive;

        return await _db.Set<Reservation>()
            .AsNoTracking()
            .Where(r =>
                r.ReservationDate >= first &&
                r.ReservationDate < last)
            .OrderBy(r => r.ReservationDate)
            .ThenBy(r => r.TimeSlot)
            .ThenBy(r => r.TableId)
            .ThenBy(r => r.ReservationId)
            .Select(r => new ReservationData(
                r.ReservationId,
                r.CustomerUserId,
                r.TableId,
                r.ReservationDate,
                r.TimeSlot,
                r.PartySize,
                r.Status,
                r.CreatedAt,
                r.SpecialRequest))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Counts bookings created during the period, whatever their dining date.
    /// </summary>
    private async Task<Dictionary<int, int>> LoadBookingsCreatedAsync(
        ReportDateRange range,
        CancellationToken ct)
    {
        var start = range.FromStored;
        var end = range.ToExclusiveStored;

        var counts = await _db.Set<Reservation>()
            .AsNoTracking()
            .Where(r => r.CreatedAt >= start && r.CreatedAt < end)
            .GroupBy(r => r.CustomerUserId)
            .Select(g => new
            {
                CustomerUserId = g.Key,
                Count = g.Count()
            })
            .ToListAsync(ct);

        return counts.ToDictionary(
            c => c.CustomerUserId,
            c => c.Count);
    }

    /// <summary>
    /// Counts only currently paid purchases.
    /// Fully refunded purchases are excluded from their original payment period.
    /// </summary>
    private async Task<List<SoldItemData>> LoadSoldItemsAsync(
        ReportDateRange range,
        CancellationToken ct)
    {
        var start = range.FromStored;
        var end = range.ToExclusiveStored;

        return await (
            from p in _db.Payments.AsNoTracking()
            where p.PaymentStatus == PaymentStatus.Paid
                  && p.PaidAt >= start
                  && p.PaidAt < end
            join i in _db.Set<OrderItem>()
                on p.OrderId equals i.OrderId
            group i by i.MenuItemId into g
            select new SoldItemData(
                g.Key,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.Quantity * i.UnitPrice)))
            .ToListAsync(ct);
    }

    /// <summary>Loads at most one opening-hours result per weekday.</summary>
    private async Task<Dictionary<DayOfWeek, OpeningHours?>> LoadOpeningHoursAsync(
        ReportDateRange range)
    {
        var hours = new Dictionary<DayOfWeek, OpeningHours?>();

        for (var i = 0; i < range.Days && hours.Count < 7; i++)
        {
            var day = range.From.AddDays(i).DayOfWeek;

            if (!hours.ContainsKey(day))
            {
                hours[day] = await _restaurant.GetOpeningHoursAsync(day);
            }
        }

        return hours;
    }

}
