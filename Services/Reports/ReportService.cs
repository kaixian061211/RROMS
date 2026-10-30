using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Reports;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Services.Reports;

public class ReportService : IReportService
{
    private const string SalesBasis =
        "Collections are counted on the date the payment was received and refunds on the date they were refunded, " +
        "so a refunded payment stays in the period it was collected. Pending and failed payments are not included. " +
        "Net collections are before processor fees; they are not profit or a Stripe payout.";

    private const string OrdersBasis =
        "Orders are counted by the date they were placed. Order value is the saved item price times quantity, " +
        "and leaves out cancelled orders.";

    private const string ReservationsBasis =
        "Scheduled reservations are counted by reservation date; new bookings by the date the booking was made. " +
        "Cancellation percentage is the share of scheduled reservations whose current status is cancelled, so it can " +
        "change if reservations are later rescheduled or cancelled. Peak reserved dining times count non-cancelled " +
        "reservations by time slot. Reservations do not show when guests actually sat down or left.";

    private const string MenuBasis =
        "Items are counted through orders whose payment is currently paid, using the original payment date. " +
        "Fully refunded purchases are excluded, including from their original payment period, so rankings can change " +
        "after a later refund. Amounts use the price saved when the order was placed. " +
        "This shows recorded purchases, not what was served.";

    private const string CustomersBasis =
        "Customers are shown by account number. Reservations and orders are counted by the date they were created; " +
        "spending uses payment and refund dates. Returning is an estimate: two or more different order dates in the " +
        "period, among orders with a paid or refunded payment. RROMS has no visit record yet.";

    private readonly AppDbContext _db;
    private readonly IRestaurantLookup _restaurant;
    private readonly IMenuItemLookup _menu;

    public ReportService(
        AppDbContext db,
        IRestaurantLookup restaurant,
        IMenuItemLookup menu)
    {
        _db = db;
        _restaurant = restaurant;
        _menu = menu;
    }

    public async Task<DashboardReport> BuildAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var range = ReportDateRange.Create(filter);

        var tables = await _restaurant.GetTablesAsync();
        var openingHours = await LoadOpeningHoursAsync(range);

        var payments = await LoadPaymentsAsync(range, cancellationToken);
        var orders = await LoadOrdersAsync(range, cancellationToken);
        var reservations = await LoadReservationsAsync(range, cancellationToken);
        var bookingsCreated = await LoadBookingsCreatedAsync(range, cancellationToken);
        var soldItems = await LoadSoldItemsAsync(range, cancellationToken);

        var menu = await _menu.GetByIdsAsync(
            soldItems.Select(item => item.MenuItemId));

        return new DashboardReport(
            filter.Period,
            range.Label,
            range.From,
            range.ToExclusive.AddDays(-1),
            ReportDateRange.NowLocal(),
            BuildSales(range, payments),
            BuildOrders(range, orders, tables),
            BuildReservations(range, reservations, bookingsCreated, tables),
            BuildUtilization(range, reservations, tables, openingHours),
            BuildMenu(soldItems, menu),
            BuildCustomers(range, payments, orders, bookingsCreated),
            BuildNotes());
    }

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

    private static bool IsCollection(
        ReportDateRange range,
        PaymentData payment) =>
        (payment.Status == PaymentStatus.Paid ||
         payment.Status == PaymentStatus.Refunded)
        && range.ContainsStored(payment.PaidAt);

    private static bool IsRefund(
        ReportDateRange range,
        PaymentData payment) =>
        payment.Status == PaymentStatus.Refunded
        && range.ContainsStored(payment.RefundedAt);

    private static SalesReport BuildSales(
        ReportDateRange range,
        IReadOnlyList<PaymentData> payments)
    {
        var collections = payments
            .Where(p => IsCollection(range, p))
            .ToList();

        var refunds = payments
            .Where(p => IsRefund(range, p))
            .ToList();

        var gross = collections.Sum(p => p.Amount);
        var refunded = refunds.Sum(p => p.Amount);

        var collectedByBucket = collections.ToLookup(p =>
            range.BucketStart(
                ReportDateRange.ToLocal(p.PaidAt!.Value)));

        var refundedByBucket = refunds.ToLookup(p =>
            range.BucketStart(
                ReportDateRange.ToLocal(p.RefundedAt!.Value)));

        var trend = range.Buckets().Select(bucket =>
        {
            var collected = collectedByBucket[bucket.Start].ToList();
            var amount = collected.Sum(p => p.Amount);
            var returned = refundedByBucket[bucket.Start].Sum(p => p.Amount);

            return new SalesTrendPoint(
                bucket.Label,
                amount,
                collected.Count,
                returned,
                amount - returned);
        }).ToList();

        // Include methods that had only refunds in this period.
        var methods = collections
            .Select(p => p.Method)
            .Concat(refunds.Select(p => p.Method))
            .Distinct()
            .Select(method =>
            {
                var collected = collections
                    .Where(p => p.Method == method)
                    .ToList();

                var amount = collected.Sum(p => p.Amount);

                var returned = refunds
                    .Where(p => p.Method == method)
                    .Sum(p => p.Amount);

                return new PaymentMethodSummary(
                    method,
                    collected.Count,
                    amount,
                    returned,
                    amount - returned,
                    Percent(amount, gross));
            })
            .OrderByDescending(m => m.Amount)
            .ThenBy(m => m.Method)
            .ToList();

        var ledger = collections
            .Select(p => new SalesTransaction(
                ReportDateRange.ToLocal(p.PaidAt!.Value),
                SalesTransactionType.Payment,
                p.PaymentId,
                p.OrderId,
                p.CustomerUserId,
                p.Method,
                p.Amount,
                p.Reference,
                null))
            .Concat(refunds.Select(p => new SalesTransaction(
                ReportDateRange.ToLocal(p.RefundedAt!.Value),
                SalesTransactionType.Refund,
                p.PaymentId,
                p.OrderId,
                p.CustomerUserId,
                p.Method,
                -p.Amount,
                p.Reference,
                p.RefundReason)))
            .OrderBy(t => t.At)
            .ThenBy(t => t.PaymentId)
            .ThenBy(t => t.Type)
            .ToList();

        return new SalesReport(
            gross,
            collections.Count,
            refunded,
            refunds.Count,
            gross - refunded,
            collections.Count == 0
                ? null
                : Math.Round(
                    gross / collections.Count,
                    2,
                    MidpointRounding.AwayFromZero),
            trend,
            methods,
            ledger,
            SalesBasis);
    }

    private static OrderReport BuildOrders(
        ReportDateRange range,
        IReadOnlyList<OrderData> orders,
        IReadOnlyDictionary<int, TableInfo> tables)
    {
        var active = orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .ToList();

        var orderValue = active.Sum(o => o.Total);

        var byStatus = orders
            .GroupBy(o => o.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var statusBreakdown = OrderStatus.All
            .Select(s => new StatusCount(
                s,
                byStatus.GetValueOrDefault(s)))
            .ToList();

        var byBucket = orders.ToLookup(o =>
            range.BucketStart(
                ReportDateRange.ToLocal(o.CreatedAt)));

        var trend = range.Buckets().Select(bucket =>
        {
            var inBucket = byBucket[bucket.Start].ToList();

            return new OrderTrendPoint(
                bucket.Label,
                inBucket.Count,
                inBucket
                    .Where(o => o.Status != OrderStatus.Cancelled)
                    .Sum(o => o.Total));
        }).ToList();

        var rows = orders.Select(o => new OrderRow(
            o.OrderId,
            ReportDateRange.ToLocal(o.CreatedAt),
            o.CustomerUserId,
            TableNumber(tables, o.TableId),
            o.Status,
            o.ItemCount,
            o.Total,
            o.PaymentStatus)).ToList();

        return new OrderReport(
            orders.Count,
            orders.Count - active.Count,
            orderValue,
            active.Count == 0
                ? null
                : Math.Round(
                    orderValue / active.Count,
                    2,
                    MidpointRounding.AwayFromZero),
            statusBreakdown,
            trend,
            rows,
            OrdersBasis);
    }

    private static ReservationReport BuildReservations(
        ReportDateRange range,
        IReadOnlyList<ReservationData> reservations,
        IReadOnlyDictionary<int, int> bookingsCreated,
        IReadOnlyDictionary<int, TableInfo> tables)
    {
        var active = reservations
            .Where(r => r.Status != ReservationStatus.Cancelled)
            .ToList();

        var cancelled = reservations.Count - active.Count;

        var byStatus = reservations
            .GroupBy(r => r.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var statusBreakdown = ReservationStatus.All
            .Select(s => new StatusCount(
                s,
                byStatus.GetValueOrDefault(s)))
            .ToList();

        var byBucket = reservations.ToLookup(r =>
            range.BucketStart(
                r.Date.ToDateTime(TimeOnly.MinValue),
                byTime: false));

        var trend = range.Buckets(byTime: false).Select(bucket =>
        {
            var inBucket = byBucket[bucket.Start].ToList();

            return new ReservationTrendPoint(
                bucket.Label,
                inBucket.Count,
                inBucket.Count(r =>
                    r.Status == ReservationStatus.Cancelled));
        }).ToList();

        // Keep exact seating times and include configured slots with no bookings.
        var perSlot = ReservationTimeSlots.All
            .Concat(active.Select(r => r.Slot))
            .Distinct()
            .OrderBy(s => s)
            .Select(slot => (
                Slot: slot,
                Items: active.Where(r => r.Slot == slot).ToList()))
            .ToList();

        var busiest = perSlot.Max(s => s.Items.Count);

        var slots = perSlot.Select(s => new ReservationSlotSummary(
            s.Slot,
            s.Items.Count,
            s.Items.Sum(r => r.PartySize),
            busiest > 0 && s.Items.Count == busiest))
            .ToList();

        var rows = reservations.Select(r => new ReservationRow(
            r.ReservationId,
            r.Date,
            r.Slot,
            TableNumber(tables, r.TableId),
            AreaName(tables, r.TableId),
            r.CustomerUserId,
            r.PartySize,
            r.Status,
            ReportDateRange.ToLocal(r.CreatedAt),
            r.SpecialRequest)).ToList();

        return new ReservationReport(
            reservations.Count,
            active.Sum(r => r.PartySize),
            statusBreakdown,
            bookingsCreated.Values.Sum(),
            cancelled,
            Percent(cancelled, reservations.Count),
            trend,
            slots,
            rows,
            ReservationsBasis);
    }

    /// <summary>
    /// Applies current active tables and opening hours to the whole period.
    /// Historical configuration is unavailable, so this remains an estimate.
    /// </summary>
    private static TableUtilizationReport BuildUtilization(
        ReportDateRange range,
        IReadOnlyList<ReservationData> reservations,
        IReadOnlyDictionary<int, TableInfo> tables,
        IReadOnlyDictionary<DayOfWeek, OpeningHours?> openingHours)
    {
        var seatingsByDay = openingHours.ToDictionary(
            h => h.Key,
            h => SeatingsOpen(h.Value));

        var tablesInUse = tables.Values
            .Where(t => t.IsActive)
            .OrderBy(t => t.TableNumber.Length)
            .ThenBy(
                t => t.TableNumber,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tableIds = tablesInUse
            .Select(t => t.TableId)
            .ToHashSet();

        // Every active table has the same available seatings on a given date.
        var slotsPerTable = Enumerable.Range(0, range.Days)
            .Sum(i => seatingsByDay[
                range.From.AddDays(i).DayOfWeek].Count);

        var booked = reservations
            .Where(r => r.Status != ReservationStatus.Cancelled)
            .ToList();

        var bookedSlots = booked
            .Select(r => (r.TableId, r.Date, r.Slot))
            .Distinct()
            .ToList();

        var matched = bookedSlots
            .Where(s =>
                tableIds.Contains(s.TableId) &&
                seatingsByDay[s.Date.DayOfWeek].Contains(s.Slot))
            .ToList();

        var matchedByTable = matched.ToLookup(s => s.TableId);

        var rows = tablesInUse.Select(t =>
        {
            var count = matchedByTable[t.TableId].Count();

            return new TableUtilizationRow(
                t.TableId,
                t.TableNumber,
                t.AreaName,
                t.Capacity,
                slotsPerTable,
                count,
                Percent(count, slotsPerTable));
        }).ToList();

        var available = slotsPerTable * tablesInUse.Count;

        var basis =
            "Estimated booked-slot utilization using current configuration. " +
            "A slot is one table on one date at one of the fixed seatings (" +
            string.Join(
                ", ",
                ReservationTimeSlots.All.Select(s => s.ToString("HH:mm"))) +
            "). Available slots are today's active tables on the days and " +
            "seatings that today's opening hours allow, applied to every date " +
            "in the period; the restaurant module does not keep a history of " +
            "tables or hours. Cancelled reservations release their slot. " +
            "Party size is not counted, so this is not seat utilization.";

        return new TableUtilizationReport(
            available,
            matched.Count,
            Percent(matched.Count, available),
            booked.Count - bookedSlots.Count,
            bookedSlots.Count - matched.Count,
            rows,
            basis);
    }

    /// <summary>
    /// Mirrors the reservation service:
    /// closed means no seatings; missing hours allow all configured seatings.
    /// </summary>
    private static HashSet<TimeOnly> SeatingsOpen(OpeningHours? hours) =>
        hours switch
        {
            { IsClosed: true } => new HashSet<TimeOnly>(),

            { Opens: { } opens, Closes: { } closes } =>
                ReservationTimeSlots.All
                    .Where(s => s >= opens && s < closes)
                    .ToHashSet(),

            _ => ReservationTimeSlots.All.ToHashSet()
        };

    private static PopularMenuReport BuildMenu(
        IReadOnlyList<SoldItemData> soldItems,
        IReadOnlyDictionary<int, MenuItemInfo> menu)
    {
        var totalAmount = soldItems.Sum(s => s.Amount);

        var items = soldItems
            .OrderByDescending(s => s.Quantity)
            .ThenBy(s => s.MenuItemId)
            .Select((s, index) =>
            {
                var info = menu.GetValueOrDefault(s.MenuItemId);

                return new PopularMenuItemSummary(
                    index + 1,
                    s.MenuItemId,
                    info?.Name ?? $"Menu item #{s.MenuItemId}",
                    info?.CategoryName ?? "Unknown",
                    s.Quantity,
                    s.Amount,
                    Percent(s.Amount, totalAmount));
            })
            .ToList();

        return new PopularMenuReport(
            soldItems.Sum(s => s.Quantity),
            totalAmount,
            items,
            MenuBasis);
    }

    private static CustomerActivityReport BuildCustomers(
        ReportDateRange range,
        IReadOnlyList<PaymentData> payments,
        IReadOnlyList<OrderData> orders,
        IReadOnlyDictionary<int, int> bookingsCreated)
    {
        // Payments without an order remain in sales but have no known customer.
        var collections = payments
            .Where(p =>
                p.CustomerUserId != null &&
                IsCollection(range, p))
            .ToLookup(p => p.CustomerUserId!.Value);

        var refunds = payments
            .Where(p =>
                p.CustomerUserId != null &&
                IsRefund(range, p))
            .ToLookup(p => p.CustomerUserId!.Value);

        var ordersByCustomer = orders.ToLookup(o => o.CustomerUserId);

        var customerIds = bookingsCreated.Keys
            .Concat(ordersByCustomer.Select(g => g.Key))
            .Concat(collections.Select(g => g.Key))
            .Concat(refunds.Select(g => g.Key))
            .Distinct();

        var customers = customerIds.Select(id =>
        {
            var paid = collections[id].ToList();
            var gross = paid.Sum(p => p.Amount);
            var returned = refunds[id].Sum(p => p.Amount);

            // Several orders on one local date represent one estimated dining day.
            // A late payment does not move the order into a different dining day.
            var diningDays = ordersByCustomer[id]
                .Where(o =>
                    o.PaymentStatus is
                        PaymentStatus.Paid or PaymentStatus.Refunded)
                .Select(o =>
                    ReportDateRange.ToLocal(o.CreatedAt).Date)
                .Distinct()
                .Count();

            return new CustomerActivitySummary(
                id,
                CustomerName(id),
                bookingsCreated.GetValueOrDefault(id),
                ordersByCustomer[id].Count(),
                paid.Count,
                gross,
                returned,
                gross - returned,
                diningDays,
                diningDays >= 2);
        })
        .OrderByDescending(c => c.NetSpending)
        .ThenByDescending(c => c.OrdersPlaced)
        .ThenBy(c => c.CustomerUserId)
        .ToList();

        return new CustomerActivityReport(
            customers.Count,
            customers.Count(c => c.IsReturning),
            customers,
            CustomersBasis);
    }

    private static List<string> BuildNotes() => new()
    {
        $"Amounts are in {DashboardReport.Currency}. Dates and times are " +
        $"restaurant time ({ReportDateRange.Zone.Id}).",

        "Figures cover payments, orders and reservations recorded in RROMS. " +
        "They are not necessarily all restaurant business or Stripe payouts.",

        "Reports are read from the saved records when generated; " +
        "a later export can differ if records changed in between."
    };

    private static string CustomerName(int customerUserId) =>
        $"Customer #{customerUserId}";

    private static string TableNumber(
        IReadOnlyDictionary<int, TableInfo> tables,
        int tableId) =>
        tables.TryGetValue(tableId, out var table)
            ? table.TableNumber
            : $"#{tableId}";

    private static string AreaName(
        IReadOnlyDictionary<int, TableInfo> tables,
        int tableId) =>
        tables.TryGetValue(tableId, out var table)
            ? table.AreaName
            : "Unknown";

    /// <summary>A zero denominator means N/A.</summary>
    private static decimal? Percent(decimal part, decimal whole) =>
        whole == 0
            ? null
            : Math.Round(
                part / whole * 100,
                1,
                MidpointRounding.AwayFromZero);
}
