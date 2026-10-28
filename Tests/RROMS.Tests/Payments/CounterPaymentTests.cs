using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RROMS.Web.Data;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Notifications;
using RROMS.Web.Services.Payments;
using RROMS.Web.Services.Payments.Gateways;

namespace RROMS.Tests.Payments;

[Trait("Type", "Unit")]
public class CounterPaymentValidationTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task InvalidIdsAreRejectedBeforeOpeningTheDatabase(int paymentId, int staffId)
    {
        // No database provider: validation must finish before any database access.
        await using var db = new AppDbContext(new DbContextOptions<AppDbContext>());
        var delivery = new CounterPaymentTestServices.RecordingDelivery();
        var service = CounterPaymentTestServices.Create(db, delivery);

        var error = await Assert.ThrowsAsync<PaymentException>(
            () => service.CollectAtCounterAsync(paymentId, staffId, PaymentMethod.Cash));

        Assert.Equal("The payment or staff ID is invalid.", error.Message);
        Assert.Empty(delivery.NotificationIds);
    }
}

[Trait("Type", "Integration")]
public class CounterPaymentTests : IAsyncLifetime
{
    private const int CustomerId = 9001;
    private const int StaffId = 1;
    private int _orderId;
    private int _paymentId;

    private static AppDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(TestDatabase.ConnectionString, sql => sql.CommandTimeout(10))
            .Options);

    public async Task InitializeAsync()
    {
        await TestDatabase.PrepareOnceAsync();
        await using var db = NewContext();
        await db.Database.OpenConnectionAsync();
        TestDatabase.CheckIsTestDatabase((SqlConnection)db.Database.GetDbConnection());

        var order = new Order
        {
            CustomerUserId = CustomerId,
            TableId = 4,
            Items = { new OrderItem { MenuItemId = 7, Quantity = 1, UnitPrice = 3.80m } }
        };
        db.Add(order);
        await db.SaveChangesAsync();
        _orderId = order.OrderId;

        var payment = new Payment
        {
            OrderId = _orderId,
            Amount = 3.80m,
            PaymentMethod = PaymentMethod.PayAtCounter,
            PaymentStatus = PaymentStatus.Pending
        };
        db.Add(payment);
        await db.SaveChangesAsync();
        _paymentId = payment.PaymentId;
    }

    public async Task DisposeAsync()
    {
        await using var db = NewContext();
        await db.Database.OpenConnectionAsync();
        TestDatabase.CheckIsTestDatabase((SqlConnection)db.Database.GetDbConnection());

        // Delete only this test's rows, in foreign-key order.
        await db.Notifications.Where(n => n.PaymentId == _paymentId).ExecuteDeleteAsync();
        await db.Payments.Where(p => p.PaymentId == _paymentId).ExecuteDeleteAsync();
        await db.Set<OrderItem>().Where(i => i.OrderId == _orderId).ExecuteDeleteAsync();
        await db.Set<Order>().Where(o => o.OrderId == _orderId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task CancelledOrderCannotBeCollectedOrNotifyTheCustomer()
    {
        await using var db = NewContext();
        await db.Set<Order>().Where(o => o.OrderId == _orderId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Cancelled));
        var delivery = new CounterPaymentTestServices.RecordingDelivery();
        var service = CounterPaymentTestServices.Create(db, delivery);

        var error = await Assert.ThrowsAsync<PaymentException>(
            () => service.CollectAtCounterAsync(_paymentId, StaffId, PaymentMethod.Cash));

        Assert.Equal("Cancelled orders cannot be paid.", error.Message);
        await AssertStillPendingWithoutNotificationAsync(delivery);
    }

    [Fact]
    public async Task PaymentChangedDuringValidationCannotBeCollected()
    {
        await using var db = NewContext();
        var orders = new BeforeOrderLookup(new PaymentOrderProvider(db), async () =>
        {
            await using var changedByAnotherRequest = NewContext();
            await changedByAnotherRequest.Payments.Where(p => p.PaymentId == _paymentId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Amount, 4.80m));
        });
        var delivery = new CounterPaymentTestServices.RecordingDelivery();
        var service = CounterPaymentTestServices.Create(db, delivery, orders);

        await Assert.ThrowsAsync<PaymentException>(
            () => service.CollectAtCounterAsync(_paymentId, StaffId, PaymentMethod.Cash));

        await AssertStillPendingWithoutNotificationAsync(delivery);
    }

    [Fact]
    public async Task SuccessfulCollectionCommitsOneNotificationBeforeDelivery()
    {
        await using var db = NewContext();
        var delivery = new CounterPaymentTestServices.RecordingDelivery(async notificationId =>
        {
            Assert.Null(db.Database.CurrentTransaction);
            // A separate connection must see both committed records at delivery time.
            await using var committed = NewContext();
            var payment = await committed.Payments.SingleAsync(p => p.PaymentId == _paymentId);
            var notification = await committed.Notifications
                .SingleAsync(n => n.NotificationId == notificationId);

            Assert.Equal(PaymentStatus.Paid, payment.PaymentStatus);
            Assert.Equal(PaymentMethod.Cash, payment.PaymentMethod);
            Assert.Equal(StaffId, payment.RecordedByStaffId);
            Assert.NotNull(payment.PaidAt);
            Assert.Equal(CustomerId, notification.UserId);
            Assert.Equal(_paymentId, notification.PaymentId);
            Assert.Equal(NotificationTypes.Payment, notification.Type);
            Assert.False(notification.IsRead);
        });
        var service = CounterPaymentTestServices.Create(db, delivery);

        await service.CollectAtCounterAsync(_paymentId, StaffId, PaymentMethod.Cash);

        Assert.Single(delivery.NotificationIds);
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.PaymentId == _paymentId));
    }

    private async Task AssertStillPendingWithoutNotificationAsync(
        CounterPaymentTestServices.RecordingDelivery delivery)
    {
        await using var db = NewContext();
        var payment = await db.Payments.SingleAsync(p => p.PaymentId == _paymentId);
        Assert.Equal(PaymentStatus.Pending, payment.PaymentStatus);
        Assert.Null(payment.PaidAt);
        Assert.Null(payment.RecordedByStaffId);
        Assert.False(await db.Notifications.AnyAsync(n => n.PaymentId == _paymentId));
        Assert.Empty(delivery.NotificationIds);
    }

    private sealed class BeforeOrderLookup(IPaymentOrderProvider inner, Func<Task> beforeLookup)
        : IPaymentOrderProvider
    {
        public async Task<PaymentOrderInfo?> GetOrderAsync(int orderId)
        {
            await beforeLookup();
            return await inner.GetOrderAsync(orderId);
        }

        public Task<IReadOnlyList<PaymentOrderInfo>> GetOrdersForCustomerAsync(int customerUserId) =>
            inner.GetOrdersForCustomerAsync(customerUserId);

        public Task<IReadOnlyDictionary<int, PaymentOrderInfo>> GetOrdersAsync(IEnumerable<int> orderIds) =>
            inner.GetOrdersAsync(orderIds);

        public Task<IReadOnlyDictionary<int, string>> GetStaffNamesAsync(IEnumerable<int> staffUserIds) =>
            inner.GetStaffNamesAsync(staffUserIds);
    }
}

internal static class CounterPaymentTestServices
{
    internal static PaymentService Create(
        AppDbContext db,
        INotificationDelivery delivery,
        IPaymentOrderProvider? orderProvider = null)
    {
        var orders = orderProvider ?? new PaymentOrderProvider(db);
        var settings = Options.Create(new StripeSettings());
        return new PaymentService(
            db,
            new NotificationService(db),
            delivery,
            new StripeGateway(settings, NullLogger<StripeGateway>.Instance),
            orders,
            new PaymentQueries(db, orders),
            [],
            NullLogger<PaymentService>.Instance,
            settings);
    }

    internal sealed class RecordingDelivery(Func<int, Task>? onDelivery = null) : INotificationDelivery
    {
        internal List<int> NotificationIds { get; } = [];

        public async Task DeliverAsync(
            int notificationId,
            bool sendEmail = false,
            NotificationEmailRecipient? recipient = null,
            CancellationToken ct = default)
        {
            NotificationIds.Add(notificationId);
            if (onDelivery is not null)
                await onDelivery(notificationId);
        }
    }
}
