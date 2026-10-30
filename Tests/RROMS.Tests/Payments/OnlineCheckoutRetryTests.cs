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
using Stripe;
using Payment = RROMS.Web.Models.Payments.Payment;
using PaymentMethod = RROMS.Web.Models.Payments.PaymentMethod;

namespace RROMS.Tests.Payments;

[Trait("Type", "Integration")]
public class OnlineCheckoutRetryTests : IAsyncLifetime
{
    private const int CustomerId = 9001;
    private int _orderId;
    private int _paymentId;
    private string _sessionId = string.Empty;

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
            Items = { new OrderItem { MenuItemId = 7, Quantity = 1, UnitPrice = 25.80m } }
        };
        db.Add(order);
        await db.SaveChangesAsync();
        _orderId = order.OrderId;
        _sessionId = $"cs_test_retry_{_orderId}";

        var payment = new Payment
        {
            OrderId = _orderId,
            Amount = 25.80m,
            PaymentMethod = PaymentMethod.Card,
            PaymentStatus = PaymentStatus.Pending,
            TransactionRef = _sessionId
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
        await db.Notifications.Where(n => n.PaymentId == _paymentId).ExecuteDeleteAsync();
        await db.Payments.Where(p => p.PaymentId == _paymentId).ExecuteDeleteAsync();
        await db.Set<OrderItem>().Where(i => i.OrderId == _orderId).ExecuteDeleteAsync();
        await db.Set<Order>().Where(o => o.OrderId == _orderId).ExecuteDeleteAsync();
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Failed)]
    public async Task AlreadyPaidStripeSessionIsRecordedInsteadOfStartingAnotherCheckout(string savedStatus)
    {
        await using var db = NewContext();
        await db.Payments.Where(p => p.PaymentId == _paymentId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PaymentStatus, savedStatus));
        var original = await SavedPaymentAsync(db);
        var stripe = new RecordingStripe { Confirmation = Confirm(original) };
        var delivery = new CounterPaymentTestServices.RecordingDelivery();
        var service = CreateService(db, stripe, delivery);

        var result = await service.StartCheckoutAsync(_orderId, CustomerId, PaymentMethod.Card, "https://localhost");

        Assert.Null(result.RedirectUrl);
        Assert.Equal(_paymentId, result.Payment.PaymentId);
        Assert.Equal(PaymentStatus.Paid, result.Payment.PaymentStatus);
        var saved = await SavedPaymentAsync(db);
        Assert.Equal(PaymentStatus.Paid, saved.PaymentStatus);
        Assert.NotNull(saved.PaidAt);
        Assert.Equal(original.CreatedAt, saved.CreatedAt);
        Assert.Equal(_sessionId, saved.TransactionRef);
        Assert.Equal(stripe.Confirmation.PaymentIntentId, saved.StripePaymentIntentId);
        Assert.Equal(1, stripe.LookupCalls);
        Assert.Equal(0, stripe.ExpireCalls);
        Assert.Equal(0, stripe.CreateCalls);
        Assert.Single(delivery.NotificationIds);

        // Repeated payment clicks must preserve the committed transition and notification.
        await Assert.ThrowsAsync<PaymentException>(() =>
            service.StartCheckoutAsync(_orderId, CustomerId, PaymentMethod.Card, "https://localhost"));
        Assert.Equal(saved.PaidAt, (await SavedPaymentAsync(db)).PaidAt);
        Assert.Single(delivery.NotificationIds);
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.PaymentId == _paymentId));
        Assert.Equal(0, stripe.CreateCalls);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("attempt")]
    [InlineData("session")]
    [InlineData("payment")]
    [InlineData("order")]
    public async Task InconsistentPaidConfirmationCannotUnlockAnotherCheckout(string mismatch)
    {
        await using var db = NewContext();
        var original = await SavedPaymentAsync(db);
        var confirmation = Confirm(original);
        confirmation = mismatch switch
        {
            "amount" => confirmation with { AmountTotal = confirmation.AmountTotal + 1 },
            "currency" => confirmation with { Currency = "usd" },
            "attempt" => confirmation with { AttemptTicks = original.CreatedAt.AddTicks(1).Ticks },
            "session" => confirmation with { SessionId = "cs_test_another" },
            "payment" => confirmation with { PaymentId = _paymentId + 1 },
            "order" => confirmation with { OrderId = _orderId + 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch))
        };
        var stripe = new RecordingStripe { Confirmation = confirmation };
        var delivery = new CounterPaymentTestServices.RecordingDelivery();
        var service = CreateService(db, stripe, delivery);

        await Assert.ThrowsAsync<PaymentException>(() =>
            service.StartCheckoutAsync(_orderId, CustomerId, PaymentMethod.Card, "https://localhost"));

        await AssertPendingAttemptUnchangedAsync(db, original, stripe, delivery);
        Assert.Equal(0, stripe.ExpireCalls);
    }

    [Fact]
    public async Task StripeLookupFailurePreservesTheAttemptAndDoesNotCreateOrExpireCheckout()
    {
        await using var db = NewContext();
        var original = await SavedPaymentAsync(db);
        var stripe = new RecordingStripe
        {
            LookupError = new PaymentException("Stripe lookup interrupted.", true)
        };
        var delivery = new CounterPaymentTestServices.RecordingDelivery();
        var service = CreateService(db, stripe, delivery);

        await Assert.ThrowsAsync<PaymentException>(() =>
            service.StartCheckoutAsync(_orderId, CustomerId, PaymentMethod.Card, "https://localhost"));

        await AssertPendingAttemptUnchangedAsync(db, original, stripe, delivery);
        Assert.Equal(0, stripe.ExpireCalls);
    }

    [Fact]
    public async Task UnpaidSessionThatCannotExpireStillPreventsAnotherCheckout()
    {
        await using var db = NewContext();
        var original = await SavedPaymentAsync(db);
        var stripe = new RecordingStripe { CanExpire = false };
        var delivery = new CounterPaymentTestServices.RecordingDelivery();
        var service = CreateService(db, stripe, delivery);

        await Assert.ThrowsAsync<PaymentException>(() =>
            service.StartCheckoutAsync(_orderId, CustomerId, PaymentMethod.Card, "https://localhost"));

        await AssertPendingAttemptUnchangedAsync(db, original, stripe, delivery);
        Assert.True(stripe.ExpireCalls > 0);
    }

    [Fact]
    public async Task ConfirmedExpiryAllowsOneNewCheckoutAttempt()
    {
        await using var db = NewContext();
        var original = await SavedPaymentAsync(db);
        var stripe = new RecordingStripe { CanExpire = true };
        var delivery = new CounterPaymentTestServices.RecordingDelivery();
        var service = CreateService(db, stripe, delivery);

        var result = await service.StartCheckoutAsync(_orderId, CustomerId, PaymentMethod.Card, "https://localhost");

        Assert.Equal("https://checkout.stripe.test/retry", result.RedirectUrl);
        var saved = await SavedPaymentAsync(db);
        Assert.Equal(_paymentId, saved.PaymentId);
        Assert.Equal(PaymentStatus.Pending, saved.PaymentStatus);
        Assert.True(saved.CreatedAt > original.CreatedAt);
        Assert.Equal("cs_test_replacement", saved.TransactionRef);
        Assert.Equal(1, stripe.ExpireCalls);
        Assert.Equal(1, stripe.CreateCalls);
        Assert.Empty(delivery.NotificationIds);
    }

    [Fact]
    public async Task CounterPaymentChangesBypassStripe()
    {
        await using var db = NewContext();
        await db.Payments.Where(p => p.PaymentId == _paymentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.PaymentMethod, PaymentMethod.PayAtCounter)
                .SetProperty(p => p.TransactionRef, (string?)null));
        var stripe = new RecordingStripe();
        var delivery = new CounterPaymentTestServices.RecordingDelivery();
        var service = CreateService(db, stripe, delivery);

        var result = await service.StartCheckoutAsync(_orderId, CustomerId, PaymentMethod.PayAtCounter, "https://localhost");

        Assert.Null(result.RedirectUrl);
        Assert.Equal(PaymentStatus.Pending, result.Payment.PaymentStatus);
        Assert.Equal(PaymentMethod.PayAtCounter, result.Payment.PaymentMethod);
        Assert.Equal(0, stripe.LookupCalls);
        Assert.Equal(0, stripe.ExpireCalls);
        Assert.Equal(0, stripe.CreateCalls);
        Assert.Empty(delivery.NotificationIds);
    }

    private Task<Payment> SavedPaymentAsync(AppDbContext db) =>
        db.Payments.AsNoTracking().SingleAsync(p => p.PaymentId == _paymentId);

    private OnlinePaymentConfirmation Confirm(Payment payment) => new(
        payment.PaymentId, payment.OrderId, _sessionId, $"pi_test_retry_{_orderId}",
        2580, "myr", payment.CreatedAt.Ticks);

    private async Task AssertPendingAttemptUnchangedAsync(
        AppDbContext db,
        Payment original,
        RecordingStripe stripe,
        CounterPaymentTestServices.RecordingDelivery delivery)
    {
        var saved = await SavedPaymentAsync(db);
        Assert.Equal(PaymentStatus.Pending, saved.PaymentStatus);
        Assert.Equal(original.CreatedAt, saved.CreatedAt);
        Assert.Equal(original.TransactionRef, saved.TransactionRef);
        Assert.Null(saved.PaidAt);
        Assert.Equal(0, stripe.CreateCalls);
        Assert.False(await db.Notifications.AnyAsync(n => n.PaymentId == _paymentId));
        Assert.Empty(delivery.NotificationIds);
    }

    private static PaymentService CreateService(
        AppDbContext db, RecordingStripe stripe, INotificationDelivery delivery)
    {
        var orders = new PaymentOrderProvider(db);
        return new PaymentService(
            db,
            new NotificationService(db, new NoopRealtime()),
            delivery,
            stripe,
            orders,
            new PaymentQueries(db, orders),
            [],
            NullLogger<PaymentService>.Instance,
            Options.Create(new StripeSettings { Currency = "myr" }));
    }

    private sealed class NoopRealtime : INotificationRealtime
    {
        public Task PublishAsync(Notification notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task PublishReadStateChangedAsync(int userId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class RecordingStripe : IStripeGateway
    {
        public OnlinePaymentConfirmation? Confirmation { get; init; }
        public PaymentException? LookupError { get; init; }
        public bool CanExpire { get; init; }
        public int LookupCalls { get; private set; }
        public int ExpireCalls { get; private set; }
        public int CreateCalls { get; private set; }

        public Task<OnlinePaymentConfirmation?> GetPaidConfirmationAsync(string sessionId)
        {
            LookupCalls++;
            if (LookupError is not null)
                throw LookupError;
            return Task.FromResult(Confirmation);
        }

        public Task<bool> TryExpireSessionAsync(string sessionId)
        {
            ExpireCalls++;
            return Task.FromResult(CanExpire);
        }

        public Task<StripeSessionInfo> CreateCheckoutSessionAsync(
            Payment payment, string customerEmail, string successUrl, string cancelUrl)
        {
            CreateCalls++;
            return Task.FromResult(new StripeSessionInfo("cs_test_replacement", "https://checkout.stripe.test/retry"));
        }

        public Task<string> RefundAsync(string paymentIntentId, decimal amount, string idempotencyKey) =>
            throw new NotSupportedException();
        public Task<string> RecheckRefundAsync(string paymentIntentId, decimal amount) =>
            throw new NotSupportedException();
        public Event ConstructWebhookEvent(string json, string signatureHeader) =>
            throw new NotSupportedException();
    }
}
