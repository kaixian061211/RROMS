using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Reports;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Reports;
using RROMS.Web.Services.Restaurant;

namespace RROMS.Tests.Reports;

/// <summary>
/// Exercises the real report queries and lookups against RROMS_Tests.
/// Fixture timestamps represent restaurant-local times converted to UTC.
/// </summary>
[Trait("Type", "Integration")]
public class ReportServiceTests : DatabaseTest
{
    private static readonly DateOnly Monday = new(2026, 3, 2);

    private static DateTime At(
        int year,
        int month,
        int day,
        int hour,
        int minute = 0,
        int second = 0) =>
        ReportDateRange.ToStored(
            new DateTime(year, month, day, hour, minute, second));

    private Task<DashboardReport> BuildAsync(
        ReportPeriod period,
        int year,
        int month,
        int day) =>
        BuildAsync(period, new DateOnly(year, month, day));

    private Task<DashboardReport> BuildAsync(
        ReportPeriod period,
        DateOnly date)
    {
        var db = NewContext();

        var service = new ReportService(
            db,
            new RestaurantLookup(db),
            new MenuItemLookup(db));

        return service.BuildAsync(new ReportFilter(period, date));
    }

    private async Task<int> AddMenuItemAsync(
        string name,
        decimal currentPrice)
    {
        var db = NewContext();

        var item = new MenuItem
        {
            Name = name,
            Description = "Test dish",
            Price = currentPrice
        };

        db.Add(item);
        await db.SaveChangesAsync();

        return item.MenuItemId;
    }

    private async Task<int> AddOrderAsync(
        int customer,
        DateTime created,
        string status = OrderStatus.Completed,
        params (int MenuItemId, int Quantity, decimal UnitPrice)[] items)
    {
        var order = new Order
        {
            CustomerUserId = customer,
            TableId = 1,
            Status = status,
            CreatedAt = created
        };

        foreach (var (menuItemId, quantity, unitPrice) in items)
        {
            order.Items.Add(new OrderItem
            {
                MenuItemId = menuItemId,
                Quantity = quantity,
                UnitPrice = unitPrice
            });
        }

        var db = NewContext();
        db.Add(order);
        await db.SaveChangesAsync();

        return order.OrderId;
    }

    private async Task AddPaymentAsync(
        int orderId,
        decimal amount,
        string method,
        string status,
        DateTime? paid,
        DateTime? refunded = null)
    {
        var db = NewContext();

        db.Payments.Add(new Payment
        {
            OrderId = orderId,
            Amount = amount,
            PaymentMethod = method,
            PaymentStatus = status,
            CreatedAt = paid
                ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PaidAt = paid,
            RefundedAt = refunded,
            RefundReason = refunded is null ? null : "Test refund"
        });

        await db.SaveChangesAsync();
    }

    private Task AddBookingAsync(
        int table,
        TimeOnly slot,
        string status) =>
        AddReservationAsync(Alice, table, Monday, slot, status);

    private async Task AddDatedBookingAsync(
        int customer,
        DateOnly diningDate,
        DateTime created)
    {
        var db = NewContext();

        db.Add(new Reservation
        {
            CustomerUserId = customer,
            TableId = 1,
            ReservationDate = diningDate,
            TimeSlot = new TimeOnly(12, 0),
            PartySize = 2,
            Status = ReservationStatus.Confirmed,
            CreatedAt = created
        });

        await db.SaveChangesAsync();
    }

    [Fact(DisplayName = "TC-RP-11 Refunds use their own period")]
    public async Task RefundStaysOutOfTheCollectionMonth()
    {
        var order = await AddOrderAsync(
            Alice,
            At(2026, 1, 15, 12));

        await AddPaymentAsync(
            order,
            100m,
            PaymentMethod.Card,
            PaymentStatus.Refunded,
            At(2026, 1, 15, 18),
            At(2026, 2, 10, 18));

        var january = (await BuildAsync(
            ReportPeriod.Monthly, 2026, 1, 10)).Sales;

        Assert.Equal(100m, january.GrossCollections);
        Assert.Equal(1, january.PaymentCount);
        Assert.Equal(0m, january.ConfirmedRefunds);
        Assert.Equal(100m, january.NetCollections);
        Assert.Equal(100m, january.AveragePayment);

        var february = (await BuildAsync(
            ReportPeriod.Monthly, 2026, 2, 10)).Sales;

        Assert.Equal(0m, february.GrossCollections);
        Assert.Equal(0, february.PaymentCount);
        Assert.Equal(100m, february.ConfirmedRefunds);
        Assert.Equal(1, february.RefundCount);
        Assert.Equal(-100m, february.NetCollections);
        Assert.Null(february.AveragePayment);

        Assert.Equal(28, february.Trend.Count);
        Assert.Equal(-100m, february.Trend.Sum(p => p.Net));

        var method = Assert.Single(february.Methods);
        Assert.Equal(PaymentMethod.Card, method.Method);
        Assert.Equal(-100m, method.Net);
        Assert.Null(method.SharePercent);

        var refund = Assert.Single(february.Transactions);
        Assert.Equal(SalesTransactionType.Refund, refund.Type);
        Assert.Equal(-100m, refund.Amount);
    }

    [Fact(DisplayName = "TC-RP-12 Pending and failed payments are excluded")]
    public async Task PendingAndFailedAreExcluded()
    {
        var paid = await AddOrderAsync(Alice, At(2026, 3, 5, 12));
        var pending = await AddOrderAsync(Alice, At(2026, 3, 5, 12));
        var failed = await AddOrderAsync(Alice, At(2026, 3, 5, 12));
        var counter = await AddOrderAsync(Alice, At(2026, 3, 5, 12));

        await AddPaymentAsync(
            paid, 25m, PaymentMethod.Cash,
            PaymentStatus.Paid, At(2026, 3, 5, 13));

        await AddPaymentAsync(
            pending, 50m, PaymentMethod.Card,
            PaymentStatus.Pending, null);

        await AddPaymentAsync(
            failed, 70m, PaymentMethod.Card,
            PaymentStatus.Failed, null);

        await AddPaymentAsync(
            counter, 30m, PaymentMethod.PayAtCounter,
            PaymentStatus.Pending, null);

        var sales = (await BuildAsync(
            ReportPeriod.Monthly, 2026, 3, 1)).Sales;

        Assert.Equal(25m, sales.GrossCollections);
        Assert.Equal(1, sales.PaymentCount);
        Assert.Single(sales.Transactions);
    }

    [Fact(DisplayName = "TC-RP-13 UTC payment boundaries follow Malaysia days")]
    public async Task UtcBoundariesAreInclusiveStartExclusiveEnd()
    {
        // Malaysia 29 October starts at 28 October 16:00 UTC.
        // Raw UTC values keep this check independent of the At helper.
        await AddPaymentAsync(
            1, 5m, PaymentMethod.Cash, PaymentStatus.Paid,
            new DateTime(2026, 10, 28, 15, 59, 59, DateTimeKind.Utc));

        await AddPaymentAsync(
            2, 10m, PaymentMethod.Cash, PaymentStatus.Paid,
            new DateTime(2026, 10, 28, 16, 0, 0, DateTimeKind.Utc));

        await AddPaymentAsync(
            3, 20m, PaymentMethod.Cash, PaymentStatus.Paid,
            new DateTime(2026, 10, 29, 16, 0, 0, DateTimeKind.Utc));

        var sales = (await BuildAsync(
            ReportPeriod.Daily, 2026, 10, 29)).Sales;

        Assert.Equal(10m, sales.GrossCollections);
        Assert.Equal(1, sales.PaymentCount);
        Assert.Equal(10m, sales.Trend[0].Collections);

        // Sales survive a missing order; customer attribution remains unknown.
        var transaction = Assert.Single(sales.Transactions);
        Assert.Null(transaction.CustomerUserId);
        Assert.Equal(
            new DateTime(2026, 10, 29, 0, 0, 0),
            transaction.At);
    }

    [Fact(DisplayName = "TC-RP-14 Sales totals reconcile across report sections")]
    public async Task SalesTotalsAgree()
    {
        var card = await AddOrderAsync(Alice, At(2026, 3, 5, 12));
        var wallet = await AddOrderAsync(Bob, At(2026, 3, 6, 12));
        var refunded = await AddOrderAsync(Bob, At(2026, 3, 7, 12));

        await AddPaymentAsync(
            card, 60m, PaymentMethod.Card,
            PaymentStatus.Paid, At(2026, 3, 5, 13));

        await AddPaymentAsync(
            wallet, 40m, PaymentMethod.EWallet,
            PaymentStatus.Paid, At(2026, 3, 6, 13));

        await AddPaymentAsync(
            refunded, 25m, PaymentMethod.Card,
            PaymentStatus.Refunded,
            At(2026, 3, 7, 13),
            At(2026, 3, 8, 13));

        var sales = (await BuildAsync(
            ReportPeriod.Monthly, 2026, 3, 1)).Sales;

        Assert.Equal(125m, sales.GrossCollections);
        Assert.Equal(25m, sales.ConfirmedRefunds);
        Assert.Equal(100m, sales.NetCollections);
        Assert.Equal(41.67m, sales.AveragePayment);

        Assert.Equal(
            sales.GrossCollections,
            sales.Methods.Sum(m => m.Amount));

        Assert.Equal(
            sales.NetCollections,
            sales.Methods.Sum(m => m.Net));

        Assert.Equal(
            sales.NetCollections,
            sales.Transactions.Sum(t => t.Amount));

        Assert.Equal(
            sales.NetCollections,
            sales.Trend.Sum(t => t.Net));

        Assert.Equal(4, sales.Transactions.Count);
    }

    [Fact(DisplayName = "TC-RP-15 Menu rankings use saved prices and exclude refunds")]
    public async Task PopularItemsUseSavedPricesAndExcludeRefunds()
    {
        var laksa = await AddMenuItemAsync("Laksa", 99m);
        var tea = await AddMenuItemAsync("Teh tarik", 3m);

        var paid = await AddOrderAsync(
            Alice, At(2026, 3, 5, 12), OrderStatus.Completed,
            (laksa, 2, 12m),
            (tea, 3, 3m));

        var refunded = await AddOrderAsync(
            Bob, At(2026, 3, 6, 12), OrderStatus.Completed,
            (laksa, 1, 12m));

        var unpaid = await AddOrderAsync(
            Bob, At(2026, 3, 7, 12), OrderStatus.Completed,
            (tea, 10, 3m));

        await AddPaymentAsync(
            paid, 33m, PaymentMethod.Card,
            PaymentStatus.Paid, At(2026, 3, 5, 13));

        await AddPaymentAsync(
            refunded, 12m, PaymentMethod.Card,
            PaymentStatus.Refunded,
            At(2026, 3, 6, 13),
            At(2026, 3, 7, 13));

        await AddPaymentAsync(
            unpaid, 30m, PaymentMethod.Card,
            PaymentStatus.Pending, null);

        var report = await BuildAsync(
            ReportPeriod.Monthly, 2026, 3, 1);

        // Refunded collections remain in sales, but not in menu rankings.
        Assert.Equal(45m, report.Sales.GrossCollections);
        Assert.Equal(12m, report.Sales.ConfirmedRefunds);
        Assert.Equal(33m, report.Sales.NetCollections);

        Assert.Equal(33m, report.Menu.TotalAmount);
        Assert.Equal(5, report.Menu.TotalQuantity);
        Assert.Equal(2, report.Menu.Items.Count);

        var first = report.Menu.Items[0];
        Assert.Equal(
            (1, "Teh tarik", 3, 9m),
            (first.Rank, first.Name, first.Quantity, first.Amount));

        Assert.Equal(27.3m, first.SharePercent);

        var second = report.Menu.Items[1];
        Assert.Equal(
            (2, "Laksa", 2, 24m),
            (second.Rank, second.Name, second.Quantity, second.Amount));

        Assert.Equal(72.7m, second.SharePercent);
    }

    [Fact(DisplayName = "TC-RP-16 Missing menu entries retain item totals")]
    public async Task UnknownMenuItemGetsAFallbackName()
    {
        var order = await AddOrderAsync(
            Alice, At(2026, 3, 5, 12), OrderStatus.Completed,
            (987654, 1, 8m));

        await AddPaymentAsync(
            order, 8m, PaymentMethod.Cash,
            PaymentStatus.Paid, At(2026, 3, 5, 13));

        var menu = (await BuildAsync(
            ReportPeriod.Monthly, 2026, 3, 1)).Menu;

        var item = Assert.Single(menu.Items);

        Assert.Equal("Menu item #987654", item.Name);
        Assert.Equal("Unknown", item.Category);
        Assert.Equal(8m, item.Amount);
    }

    [Fact(DisplayName = "TC-RP-17 Cancelled reservations release booked slots")]
    public async Task BookedSlotsFollowReservationStatuses()
    {
        await AddBookingAsync(
            1, new TimeOnly(12, 0), ReservationStatus.Pending);

        await AddBookingAsync(
            1, new TimeOnly(13, 30), ReservationStatus.Confirmed);

        await AddBookingAsync(
            1, new TimeOnly(18, 0), ReservationStatus.Completed);

        await AddBookingAsync(
            1, new TimeOnly(19, 30), ReservationStatus.Cancelled);

        await AddBookingAsync(
            2, new TimeOnly(12, 0), ReservationStatus.Cancelled);

        await AddBookingAsync(
            2, new TimeOnly(12, 0), ReservationStatus.Confirmed);

        var report = await BuildAsync(ReportPeriod.Daily, Monday);

        Assert.Equal(6, report.Reservations.ScheduledReservations);
        Assert.Equal(2, report.Reservations.CancelledScheduled);
        Assert.Equal(33.3m, report.Reservations.CancellationPercent);

        Assert.Equal(50, report.Utilization.AvailableSlots);
        Assert.Equal(4, report.Utilization.BookedSlots);
        Assert.Equal(8m, report.Utilization.UtilizationPercent);
        Assert.Equal(0, report.Utilization.DuplicateBookings);
        Assert.Equal(0, report.Utilization.UnmatchedBookings);

        var peak = Assert.Single(
            report.Reservations.PeakSlots, s => s.IsPeak);

        Assert.Equal(new TimeOnly(12, 0), peak.TimeSlot);
        Assert.Equal(2, peak.Reservations);
    }

    [Fact(DisplayName = "TC-RP-18 Closed days have no available capacity")]
    public async Task ClosedDayFlagsBookingsOutsideCapacity()
    {
        await SetOpeningHoursAsync(DayOfWeek.Monday, closed: true);

        await AddBookingAsync(
            1, new TimeOnly(12, 0), ReservationStatus.Confirmed);

        var utilization = (await BuildAsync(
            ReportPeriod.Daily, Monday)).Utilization;

        Assert.Equal(0, utilization.AvailableSlots);
        Assert.Equal(0, utilization.BookedSlots);
        Assert.Null(utilization.UtilizationPercent);
        Assert.Equal(1, utilization.UnmatchedBookings);
    }

    [Fact(DisplayName = "TC-RP-19 Utilization uses current tables and hours")]
    public async Task CapacityUsesCurrentConfiguration()
    {
        await SetOpeningHoursAsync(
            DayOfWeek.Monday,
            closed: false,
            opens: new TimeOnly(12, 0),
            closes: new TimeOnly(19, 0));

        var db = NewContext();
        var retired = await db.Set<RestaurantTable>()
            .SingleAsync(t => t.RestaurantTableId == 10);

        retired.IsActive = false;
        await db.SaveChangesAsync();

        await AddBookingAsync(
            1, new TimeOnly(12, 0), ReservationStatus.Confirmed);

        await AddBookingAsync(
            2, new TimeOnly(19, 30), ReservationStatus.Confirmed);

        await AddBookingAsync(
            10, new TimeOnly(12, 0), ReservationStatus.Confirmed);

        var utilization = (await BuildAsync(
            ReportPeriod.Daily, Monday)).Utilization;

        // Nine active tables, with three available seatings each.
        Assert.Equal(27, utilization.AvailableSlots);
        Assert.Equal(1, utilization.BookedSlots);
        Assert.Equal(3.7m, utilization.UtilizationPercent);
        Assert.Equal(2, utilization.UnmatchedBookings);
        Assert.Equal(9, utilization.Tables.Count);

        Assert.Equal(
            utilization.AvailableSlots,
            utilization.Tables.Sum(t => t.AvailableSlots));

        Assert.Equal(
            utilization.BookedSlots,
            utilization.Tables.Sum(t => t.BookedSlots));

        Assert.StartsWith(
            "Estimated booked-slot utilization using current configuration",
            utilization.Basis);
    }

    [Fact(DisplayName = "TC-RP-20 Returning estimates use distinct order dates")]
    public async Task DiningDaysUseOrderDatesInsteadOfPaymentDates()
    {
        var morning = await AddOrderAsync(
            Alice, At(2026, 10, 10, 10));

        var evening = await AddOrderAsync(
            Alice, At(2026, 10, 10, 19));

        var later = await AddOrderAsync(
            Alice, At(2026, 10, 20, 12));

        var lateNight = await AddOrderAsync(
            Bob, At(2026, 10, 31, 23, 30));

        await AddPaymentAsync(
            morning, 10m, PaymentMethod.Cash,
            PaymentStatus.Paid, At(2026, 10, 10, 11));

        await AddPaymentAsync(
            evening, 20m, PaymentMethod.Cash,
            PaymentStatus.Paid, At(2026, 10, 10, 20));

        await AddPaymentAsync(
            later, 30m, PaymentMethod.Card,
            PaymentStatus.Paid, At(2026, 10, 20, 13));

        await AddPaymentAsync(
            lateNight, 40m, PaymentMethod.Card,
            PaymentStatus.Paid, At(2026, 11, 1, 1, 30));

        var october = (await BuildAsync(
            ReportPeriod.Monthly, 2026, 10, 1)).Customers;

        var alice = october.Customers.Single(
            c => c.CustomerUserId == Alice);

        Assert.Equal(3, alice.OrdersPlaced);
        Assert.Equal(3, alice.PaidOrders);
        Assert.Equal(60m, alice.GrossSpending);
        Assert.Equal(2, alice.EstimatedDiningDays);
        Assert.True(alice.IsReturning);

        var bobInOctober = october.Customers.Single(
            c => c.CustomerUserId == Bob);

        Assert.Equal(1, bobInOctober.OrdersPlaced);
        Assert.Equal(0, bobInOctober.PaidOrders);
        Assert.Equal(1, bobInOctober.EstimatedDiningDays);
        Assert.False(bobInOctober.IsReturning);
        Assert.Equal(1, october.ReturningCustomers);

        var november = (await BuildAsync(
            ReportPeriod.Monthly, 2026, 11, 1)).Customers;

        var bobInNovember = Assert.Single(november.Customers);

        Assert.Equal(Bob, bobInNovember.CustomerUserId);
        Assert.Equal(0, bobInNovember.OrdersPlaced);
        Assert.Equal(1, bobInNovember.PaidOrders);
        Assert.Equal(40m, bobInNovember.GrossSpending);
        Assert.Equal(0, bobInNovember.EstimatedDiningDays);
    }

    [Fact(DisplayName = "TC-RP-21 Refund-only customers remain visible")]
    public async Task RefundOnlyCustomerHasNegativeNetSpending()
    {
        var order = await AddOrderAsync(
            Bob, At(2026, 2, 28, 12));

        await AddPaymentAsync(
            order, 25m, PaymentMethod.Cash,
            PaymentStatus.Refunded,
            At(2026, 2, 28, 13),
            At(2026, 3, 6, 13));

        var report = await BuildAsync(
            ReportPeriod.Monthly, 2026, 3, 1);

        var customer = Assert.Single(report.Customers.Customers);

        Assert.Equal(Bob, customer.CustomerUserId);
        Assert.Equal(0, customer.OrdersPlaced);
        Assert.Equal(0, customer.PaidOrders);
        Assert.Equal(0m, customer.GrossSpending);
        Assert.Equal(25m, customer.RefundsReceived);
        Assert.Equal(-25m, customer.NetSpending);
        Assert.False(customer.IsReturning);
    }

    [Fact(DisplayName = "TC-RP-22 Order value excludes cancelled orders")]
    public async Task OrdersKeepStatusesAndSavedTotals()
    {
        var item = await AddMenuItemAsync("Test dish", 99m);

        var completed = await AddOrderAsync(
            Alice, At(2026, 3, 5, 12), OrderStatus.Completed,
            (item, 2, 12m));

        var pending = await AddOrderAsync(
            Bob, At(2026, 3, 6, 12), OrderStatus.Pending,
            (item, 1, 6m));

        await AddOrderAsync(
            Bob, At(2026, 3, 7, 12), OrderStatus.Cancelled,
            (item, 1, 9m));

        await AddPaymentAsync(
            completed, 24m, PaymentMethod.Cash,
            PaymentStatus.Paid, At(2026, 3, 5, 13));

        var orders = (await BuildAsync(
            ReportPeriod.Monthly, 2026, 3, 1)).Orders;

        Assert.Equal(3, orders.OrdersPlaced);
        Assert.Equal(1, orders.CancelledOrders);
        Assert.Equal(30m, orders.OrderValue);
        Assert.Equal(15m, orders.AverageOrderValue);
        Assert.Equal(OrderStatus.All.Count(), orders.StatusBreakdown.Count);
        Assert.Equal(3, orders.Trend.Sum(t => t.OrdersPlaced));
        Assert.Equal(30m, orders.Trend.Sum(t => t.OrderValue));

        var paidRow = orders.Orders.Single(o => o.OrderId == completed);
        Assert.Equal(24m, paidRow.Total);
        Assert.Equal(2, paidRow.ItemCount);
        Assert.Equal(PaymentStatus.Paid, paidRow.PaymentStatus);

        var unpaidRow = orders.Orders.Single(o => o.OrderId == pending);
        Assert.Null(unpaidRow.PaymentStatus);
    }

    [Fact(DisplayName = "TC-RP-23 Empty periods distinguish zero from N/A")]
    public async Task EmptyPeriodRetainsChartBuckets()
    {
        var report = await BuildAsync(ReportPeriod.Daily, Monday);

        Assert.Equal(0m, report.Sales.GrossCollections);
        Assert.Equal(0m, report.Sales.NetCollections);
        Assert.Null(report.Sales.AveragePayment);
        Assert.Empty(report.Sales.Transactions);
        Assert.Equal(24, report.Sales.Trend.Count);

        Assert.Equal(0, report.Orders.OrdersPlaced);
        Assert.Null(report.Orders.AverageOrderValue);
        Assert.Equal(24, report.Orders.Trend.Count);

        Assert.Equal(0, report.Reservations.ScheduledReservations);
        Assert.Null(report.Reservations.CancellationPercent);
        Assert.Single(report.Reservations.Trend);
        Assert.All(
            report.Reservations.PeakSlots,
            slot => Assert.False(slot.IsPeak));

        Assert.Equal(50, report.Utilization.AvailableSlots);
        Assert.Equal(0, report.Utilization.BookedSlots);
        Assert.Equal(0m, report.Utilization.UtilizationPercent);

        Assert.Empty(report.Menu.Items);
        Assert.Equal(0m, report.Menu.TotalAmount);
        Assert.Empty(report.Customers.Customers);
    }

    [Fact(DisplayName = "TC-RP-24 Unsupported report dates are rejected")]
    public async Task InvalidFilterIsRejected()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => BuildAsync(ReportPeriod.Monthly, 1999, 12, 1));
    }

    [Fact(DisplayName = "TC-RP-26 Later refunds remove original-period menu purchases")]
    public async Task LaterRefundChangesMenuWithoutRemovingOriginalCollection()
    {
        var item = await AddMenuItemAsync("Dinner", 50m);

        var order = await AddOrderAsync(
            Alice, At(2026, 1, 15, 12), OrderStatus.Completed,
            (item, 2, 50m));

        await AddPaymentAsync(
            order, 100m, PaymentMethod.Card,
            PaymentStatus.Paid, At(2026, 1, 15, 13));

        var beforeRefund = await BuildAsync(
            ReportPeriod.Monthly, 2026, 1, 1);

        Assert.Equal(100m, beforeRefund.Menu.TotalAmount);
        Assert.Equal(2, beforeRefund.Menu.TotalQuantity);

        var db = NewContext();
        var payment = await db.Payments.SingleAsync(
            p => p.OrderId == order);

        payment.PaymentStatus = PaymentStatus.Refunded;
        payment.RefundedAt = At(2026, 2, 10, 13);
        payment.RefundReason = "Full refund test";

        await db.SaveChangesAsync();

        var january = await BuildAsync(
            ReportPeriod.Monthly, 2026, 1, 1);

        var february = await BuildAsync(
            ReportPeriod.Monthly, 2026, 2, 1);

        Assert.Equal(100m, january.Sales.GrossCollections);
        Assert.Equal(100m, january.Sales.NetCollections);
        Assert.Empty(january.Menu.Items);
        Assert.Equal(0m, january.Menu.TotalAmount);

        Assert.Equal(100m, february.Sales.ConfirmedRefunds);
        Assert.Equal(-100m, february.Sales.NetCollections);
        Assert.Empty(february.Menu.Items);
    }

    [Fact(DisplayName = "TC-RP-27 Booking creation and scheduled dates stay separate")]
    public async Task BookingCreationDoesNotUseScheduledDate()
    {
        await AddDatedBookingAsync(
            Alice,
            new DateOnly(2026, 10, 29),
            At(2026, 9, 30, 12));

        await AddDatedBookingAsync(
            Bob,
            new DateOnly(2026, 11, 2),
            At(2026, 10, 29, 12));

        var report = await BuildAsync(
            ReportPeriod.Monthly, 2026, 10, 1);

        Assert.Equal(1, report.Reservations.ScheduledReservations);
        Assert.Equal(1, report.Reservations.NewBookingsCreated);

        var scheduled = Assert.Single(
            report.Reservations.Reservations);

        Assert.Equal(Alice, scheduled.CustomerUserId);
        Assert.Equal(new DateOnly(2026, 10, 29), scheduled.Date);

        // Customer booking activity uses creation date.
        var customer = Assert.Single(report.Customers.Customers);

        Assert.Equal(Bob, customer.CustomerUserId);
        Assert.Equal(1, customer.ReservationsMade);
    }
}
