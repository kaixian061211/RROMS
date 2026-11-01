using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RROMS.Web.Data;
using RROMS.Web.Models.Accounts;
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
public class PaymentWorkflowTests : IAsyncLifetime
{
    private int _orderId, _paymentId, _customerId, _staffId;
    private static AppDbContext NewContext(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(TestDatabase.ConnectionString, sql => sql.CommandTimeout(10))
            .AddInterceptors(interceptors).Options);

    public async Task InitializeAsync()
    {
        await TestDatabase.PrepareOnceAsync();
        await using var db = NewContext();
        await db.Database.OpenConnectionAsync();
        TestDatabase.CheckIsTestDatabase((SqlConnection)db.Database.GetDbConnection());
        var customer = new UserAccount { FullName = "Payment test customer", Email = $"customer-{Guid.NewGuid()}@example.test", PasswordHash = "test-only" };
        var staff = new UserAccount { FullName = "Payment test staff", Email = $"staff-{Guid.NewGuid()}@example.test", Role = UserRoles.Staff, PasswordHash = "test-only", IsActive = false };
        db.AddRange(customer, staff);
        await db.SaveChangesAsync();
        _customerId = customer.UserId;
        _staffId = staff.UserId;
        var order = new Order { CustomerUserId = _customerId, TableId = 4,
            Items = { new OrderItem { MenuItemId = 7, Quantity = 2, UnitPrice = 12.90m } } };
        db.Add(order);
        await db.SaveChangesAsync();
        _orderId = order.OrderId;
        var payment = new Payment { OrderId = _orderId, Amount = 25.80m,
            PaymentMethod = PaymentMethod.PayAtCounter, PaymentStatus = PaymentStatus.Pending };
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
        await db.Set<UserAccount>().Where(u => u.UserId == _customerId || u.UserId == _staffId).ExecuteDeleteAsync();
    }

    [Theory]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.OnlineBanking)]
    [InlineData(PaymentMethod.EWallet)]
    [InlineData(PaymentMethod.PayAtCounter)]
    public async Task CheckoutUsesTheSavedOrderTotalAndOnlyOnePaymentRow(string method)
    {
        await using var db = NewContext();
        var stripe = new PaymentTestStripe();
        var result = await Service(db, stripe).StartCheckoutAsync(_orderId, _customerId, method, "https://localhost/rroms");
        Assert.Equal(25.80m, result.Payment.Amount);
        Assert.Equal(_paymentId, result.Payment.PaymentId);
        Assert.Equal(1, await db.Payments.CountAsync(p => p.OrderId == _orderId));
        Assert.Equal(method == PaymentMethod.PayAtCounter ? 0 : 1, stripe.CreateCalls);
        if (method != PaymentMethod.PayAtCounter)
        {
            Assert.Contains("/rroms/Payments/Success", stripe.SuccessUrl);
            Assert.NotNull(result.RedirectUrl);
        }
    }

    [Fact]
    public async Task InterruptedCheckoutResumesTheSameAttemptWithoutPermittingAMethodChange()
    {
        await using var db = NewContext();
        var stripe = new PaymentTestStripe { CreateError = new PaymentException("Interrupted Stripe response", true) };
        var service = Service(db, stripe);
        await Assert.ThrowsAsync<PaymentException>(() => service.StartCheckoutAsync(_orderId, _customerId, PaymentMethod.Card, "https://localhost"));
        var interrupted = await SavedAsync();
        Assert.Equal(PaymentStatus.Pending, interrupted.PaymentStatus);
        Assert.Null(interrupted.TransactionRef);
        await Assert.ThrowsAsync<PaymentException>(() => service.StartCheckoutAsync(_orderId, _customerId, PaymentMethod.PayAtCounter, "https://localhost"));
        Assert.Equal(1, stripe.CreateCalls);
        stripe.CreateError = null;
        var recovered = await service.StartCheckoutAsync(_orderId, _customerId, PaymentMethod.Card, "https://localhost");
        Assert.Equal(interrupted.CreatedAt, recovered.Payment.CreatedAt);
        Assert.Equal(2, stripe.CreateCalls);
        Assert.Single(stripe.CreatedAttempts.Distinct());
        Assert.NotNull((await SavedAsync()).TransactionRef);
        Assert.Equal(0, stripe.ExpireCalls);
    }

    [Fact]
    public async Task OwnershipIsCheckedBeforeAnyGatewayOperation()
    {
        await using var db = NewContext();
        var stripe = new PaymentTestStripe();
        var service = Service(db, stripe);
        await Assert.ThrowsAsync<PaymentException>(() => service.StartCheckoutAsync(_orderId, _customerId + 100000, PaymentMethod.Card, "https://localhost"));
        await Assert.ThrowsAsync<PaymentException>(() => service.RefreshOnlineStatusAsync(_paymentId, _customerId + 100000));
        await Assert.ThrowsAsync<PaymentException>(() => service.CancelOnlineAttemptAsync(_paymentId, _customerId + 100000, 1));
        Assert.Equal(0, stripe.CreateCalls + stripe.LookupCalls + stripe.ExpireCalls);
    }

    [Fact]
    public async Task CounterCollectionRejectsAnAmountThatDoesNotMatchTheOrder()
    {
        await using var db = NewContext();
        await db.Payments.Where(p => p.PaymentId == _paymentId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Amount, 20m));
        await Assert.ThrowsAsync<PaymentException>(() => Service(db).CollectAtCounterAsync(_paymentId, _staffId, PaymentMethod.Cash));
        Assert.Equal(PaymentStatus.Pending, (await SavedAsync()).PaymentStatus);
        Assert.False(await db.Notifications.AnyAsync(n => n.PaymentId == _paymentId));
    }

    [Theory]
    [InlineData(PaymentMethod.Cash)]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.EWallet)]
    public async Task CounterCollectionCannotBeRecordedTwice(string method)
    {
        await using var db = NewContext();
        var service = Service(db);
        await service.CollectAtCounterAsync(_paymentId, _staffId, method);
        var paid = await SavedAsync();
        await Assert.ThrowsAsync<PaymentException>(() => service.CollectAtCounterAsync(_paymentId, _staffId, method));
        Assert.Equal(paid.PaidAt, (await SavedAsync()).PaidAt);
        Assert.Equal(method, paid.PaymentMethod);
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.PaymentId == _paymentId));
    }

    [Fact]
    public async Task NotificationSaveFailureRollsBackThePayment()
    {
        await using var db = NewContext(new BeforeSaving(() => throw new InvalidOperationException("Simulated save failure")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).CollectAtCounterAsync(_paymentId, _staffId, PaymentMethod.Cash));
        var saved = await SavedAsync();
        Assert.Equal(PaymentStatus.Pending, saved.PaymentStatus);
        Assert.Null(saved.PaidAt);
        await using var verify = NewContext();
        Assert.False(await verify.Notifications.AnyAsync(n => n.PaymentId == _paymentId));
    }

    [Fact]
    public async Task DeliveryFailureDoesNotUndoACommittedPayment()
    {
        await using var db = NewContext();
        var delivery = new CounterPaymentTestServices.RecordingDelivery(_ => throw new HttpRequestException("Simulated transport failure"));
        await Service(db, delivery: delivery).CollectAtCounterAsync(_paymentId, _staffId, PaymentMethod.Cash);
        Assert.Equal(PaymentStatus.Paid, (await SavedAsync()).PaymentStatus);
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.PaymentId == _paymentId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefundsPreserveTheOriginalCollectionAndAreRecordedOnce(bool online)
    {
        await SetPaidAsync(online);
        await using var db = NewContext();
        var stripe = new PaymentTestStripe();
        var original = await SavedAsync();
        var service = Service(db, stripe);
        await service.RefundAsync(_paymentId, _staffId, "  Duplicate order  ");
        var refunded = await SavedAsync();
        Assert.Equal(PaymentStatus.Refunded, refunded.PaymentStatus);
        Assert.Equal(original.PaidAt, refunded.PaidAt);
        Assert.Equal(25.80m, refunded.Amount);
        Assert.NotNull(refunded.RefundedAt);
        Assert.Equal(_staffId, refunded.RefundedByStaffId);
        Assert.Equal("Duplicate order", refunded.RefundReason);
        await Assert.ThrowsAsync<PaymentException>(() => service.RefundAsync(_paymentId, _staffId));
        Assert.Equal(online ? 1 : 0, stripe.RefundCalls);
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.PaymentId == _paymentId));
        var details = await new PaymentQueries(db, new PaymentOrderProvider(db)).GetDetailsAsync(_paymentId);
        Assert.Equal("Payment test staff", details!.RefundedByStaffName);
        if (!online) Assert.Equal("Payment test staff", details.StaffName);
    }

    [Fact]
    public async Task UnconfirmedRefundLeavesThePaymentPaidWithoutARefundNotification()
    {
        await SetPaidAsync(true);
        await using var db = NewContext();
        var stripe = new PaymentTestStripe { RefundError = new PaymentException("Refund pending", true) };
        await Assert.ThrowsAsync<PaymentException>(() => Service(db, stripe).RefundAsync(_paymentId, _staffId));
        var saved = await SavedAsync();
        Assert.Equal(PaymentStatus.Paid, saved.PaymentStatus);
        Assert.Null(saved.RefundedAt);
        Assert.Null(saved.RefundedByStaffId);
        Assert.False(await db.Notifications.AnyAsync(n => n.PaymentId == _paymentId));
    }

    [Fact]
    public async Task RefundReasonIsValidatedBeforeContactingStripe()
    {
        await SetPaidAsync(true);
        await using var db = NewContext();
        var stripe = new PaymentTestStripe();
        await Assert.ThrowsAsync<PaymentException>(() => Service(db, stripe).RefundAsync(_paymentId, _staffId, new string('x', 201)));
        Assert.Equal(0, stripe.RefundCalls);
        Assert.Equal(PaymentStatus.Paid, (await SavedAsync()).PaymentStatus);
    }

    [Fact]
    public async Task StaleCancellationLinkCannotCloseTheCurrentAttempt()
    {
        await using var db = NewContext();
        await SetOnlineAsync(db);
        var saved = await SavedAsync();
        var stripe = new PaymentTestStripe();
        await Assert.ThrowsAsync<PaymentException>(() => Service(db, stripe).CancelOnlineAttemptAsync(_paymentId, _customerId, saved.CreatedAt.AddTicks(-1).Ticks));
        Assert.Equal(0, stripe.ExpireCalls);
        Assert.Equal(PaymentStatus.Pending, (await SavedAsync()).PaymentStatus);
    }

    [Fact]
    public async Task VerifiedCancellationCannotAlterAPaidPayment()
    {
        await using var db = NewContext();
        await SetOnlineAsync(db);
        var pending = await SavedAsync();
        var stripe = new PaymentTestStripe();
        var service = Service(db, stripe);
        await service.CancelOnlineAttemptAsync(_paymentId, _customerId, pending.CreatedAt.Ticks);
        Assert.Equal(PaymentStatus.Failed, (await SavedAsync()).PaymentStatus);
        Assert.True(await service.MarkPaidOnlineAsync(Confirmation(pending)));
        await Assert.ThrowsAsync<PaymentException>(() => service.CancelOnlineAttemptAsync(_paymentId, _customerId, pending.CreatedAt.Ticks));
        Assert.Equal(PaymentStatus.Paid, (await SavedAsync()).PaymentStatus);
    }

    [Fact]
    public async Task DuplicateCallbacksPreservePaidAtAndOnlyOneNotification()
    {
        await using var db = NewContext();
        await SetOnlineAsync(db);
        var confirmation = Confirmation(await SavedAsync());
        var service = Service(db);
        Assert.True(await service.MarkPaidOnlineAsync(confirmation));
        var paid = await SavedAsync();
        Assert.False(await service.MarkPaidOnlineAsync(confirmation with { PaidAtUtc = confirmation.PaidAtUtc.AddSeconds(1) }));
        Assert.Equal(paid.PaidAt, (await SavedAsync()).PaidAt);
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.PaymentId == _paymentId));
    }

    [Fact]
    public async Task DelayedReturnKeepsStripeSuccessOnItsOriginalMalaysiaDate()
    {
        await using var db = NewContext();
        await SetOnlineAsync(db);
        var succeededAt = new DateTime(2026, 10, 29, 15, 59, 0, DateTimeKind.Utc);
        await db.Payments.Where(p => p.PaymentId == _paymentId).ExecuteUpdateAsync(s => s.SetProperty(p => p.CreatedAt, succeededAt.AddMinutes(-2)));
        var confirmation = Confirmation(await SavedAsync()) with { PaidAtUtc = succeededAt };
        Assert.True(await Service(db).MarkPaidOnlineAsync(confirmation));
        Assert.Equal(succeededAt, (await SavedAsync()).PaidAt);
        var queries = new PaymentQueries(db, new PaymentOrderProvider(db));
        var originalDay = await queries.SearchAsync(new PaymentFilter { Preset = DateRangePreset.Custom, From = new DateTime(2026, 10, 29), To = new DateTime(2026, 10, 29) });
        Assert.Contains(originalDay, p => p.PaymentId == _paymentId);
        var followingDay = await queries.SearchAsync(new PaymentFilter { Preset = DateRangePreset.Custom, From = new DateTime(2026, 10, 30), To = new DateTime(2026, 10, 30) });
        Assert.DoesNotContain(followingDay, p => p.PaymentId == _paymentId);
    }

    [Theory]
    [InlineData("checkout.session.completed")]
    [InlineData("checkout.session.async_payment_succeeded")]
    public async Task SignedWebhookUsesTheOriginalStripeSuccessTime(string eventType)
    {
        await using var db = NewContext();
        await SetOnlineAsync(db);
        var succeededAt = new DateTime(2026, 10, 29, 15, 59, 0, DateTimeKind.Utc);
        await db.Payments.Where(p => p.PaymentId == _paymentId).ExecuteUpdateAsync(s => s.SetProperty(p => p.CreatedAt, succeededAt.AddMinutes(-2)));
        var pending = await SavedAsync();
        var session = new Stripe.Checkout.Session
        {
            Id = pending.TransactionRef, Mode = "payment", PaymentStatus = "paid", AmountTotal = 2580,
            Currency = "myr", ClientReferenceId = _paymentId.ToString(), PaymentIntentId = $"pi_test_{_paymentId}",
            Metadata = new() { ["payment_id"] = _paymentId.ToString(), ["order_id"] = _orderId.ToString(), ["attempt_ticks"] = pending.CreatedAt.Ticks.ToString() }
        };
        // The gateway contract returns an Event only after signature verification.
        var stripe = new PaymentTestStripe { VerifiedEvent = new Event { Id = "evt_test_signed", Type = eventType, Created = succeededAt, Data = new Stripe.EventData { Object = session } } };
        var handler = new StripeWebhookHandler(stripe, Service(db, stripe), NullLogger<StripeWebhookHandler>.Instance);
        Assert.Equal(WebhookOutcome.Processed, await handler.HandleAsync("test-only", "verified-by-fake"));
        Assert.Equal(succeededAt, (await SavedAsync()).PaidAt);
        Assert.Equal(WebhookOutcome.Processed, await handler.HandleAsync("test-only", "verified-by-fake"));
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.PaymentId == _paymentId));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("local")]
    [InlineData("future")]
    [InlineData("before-attempt")]
    public async Task UnverifiedSuccessTimeCannotMarkPaymentPaid(string problem)
    {
        await using var db = NewContext();
        await SetOnlineAsync(db);
        var when = problem switch { "missing" => default, "local" => DateTime.Now, "before-attempt" => DateTime.UtcNow.AddDays(-1), _ => DateTime.UtcNow.AddDays(1) };
        Assert.False(await Service(db).MarkPaidOnlineAsync(Confirmation(await SavedAsync()) with { PaidAtUtc = when }));
        Assert.Null((await SavedAsync()).PaidAt);
        Assert.Equal(PaymentStatus.Pending, (await SavedAsync()).PaymentStatus);
    }

    [Theory]
    [InlineData("payment")]
    [InlineData("order")]
    [InlineData("customer")]
    public async Task SearchAcceptsDisplayedReferencesAndCustomerNames(string kind)
    {
        await using var db = NewContext();
        var search = kind switch { "payment" => $"RCP-{_paymentId:D6}", "order" => $"ORD-{_orderId:D6}", _ => "Payment test customer" };
        var rows = await new PaymentQueries(db, new PaymentOrderProvider(db)).SearchAsync(new PaymentFilter { Search = search });
        Assert.Equal(_paymentId, Assert.Single(rows).PaymentId);
    }

    [Fact]
    public async Task CounterVoidDoesNotCancelTheOrderOrPretendMoneyWasReceived()
    {
        await using var db = NewContext();
        await Service(db).VoidPendingAsync(_paymentId, _staffId);
        var payment = await SavedAsync();
        Assert.Equal(PaymentStatus.Failed, payment.PaymentStatus);
        Assert.Null(payment.PaidAt);
        Assert.Null(payment.RefundedAt);
        Assert.Equal(OrderStatus.Pending, (await db.Set<Order>().SingleAsync(o => o.OrderId == _orderId)).Status);
    }

    private Task<Payment> SavedAsync() => ReadSavedAsync();
    private async Task<Payment> ReadSavedAsync()
    {
        await using var db = NewContext();
        return await db.Payments.AsNoTracking().SingleAsync(p => p.PaymentId == _paymentId);
    }
    private async Task SetOnlineAsync(AppDbContext db) => await db.Payments.Where(p => p.PaymentId == _paymentId)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.PaymentMethod, PaymentMethod.Card).SetProperty(p => p.TransactionRef, $"cs_test_{_paymentId}"));
    private async Task SetPaidAsync(bool online)
    {
        await using var db = NewContext();
        await db.Payments.Where(p => p.PaymentId == _paymentId).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.PaymentStatus, PaymentStatus.Paid).SetProperty(p => p.PaidAt, DateTime.UtcNow)
            .SetProperty(p => p.PaymentMethod, online ? PaymentMethod.Card : PaymentMethod.Cash)
            .SetProperty(p => p.RecordedByStaffId, online ? (int?)null : _staffId)
            .SetProperty(p => p.TransactionRef, online ? $"cs_test_{_paymentId}" : null)
            .SetProperty(p => p.StripePaymentIntentId, online ? $"pi_test_{_paymentId}" : null));
    }
    private static OnlinePaymentConfirmation Confirmation(Payment p) => new(p.PaymentId, p.OrderId, p.TransactionRef!, $"pi_test_{p.PaymentId}", 2580, "MYR", p.CreatedAt.Ticks, DateTime.UtcNow);
    private static PaymentService Service(AppDbContext db, PaymentTestStripe? stripe = null, INotificationDelivery? delivery = null)
    {
        var orders = new PaymentOrderProvider(db);
        return new PaymentService(db, new NotificationService(db, new PaymentTestRealtime()),
            delivery ?? new CounterPaymentTestServices.RecordingDelivery(), stripe ?? new PaymentTestStripe(), orders,
            new PaymentQueries(db, orders), [], NullLogger<PaymentService>.Instance, Options.Create(new StripeSettings()));
    }
}

internal sealed class PaymentTestRealtime : INotificationRealtime
{
    public Task PublishAsync(Notification notification, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishReadStateChangedAsync(int userId, CancellationToken ct = default) => Task.CompletedTask;
}

// Every gateway operation stays in process. No test can send a charge or refund to Stripe.
internal sealed class PaymentTestStripe : IStripeGateway
{
    public int CreateCalls, LookupCalls, ExpireCalls, RefundCalls;
    public string? SuccessUrl;
    public Event? VerifiedEvent { get; init; }
    public PaymentException? CreateError { get; set; }
    public List<long> CreatedAttempts { get; } = [];
    public PaymentException? RefundError { get; init; }
    public Task<StripeSessionInfo> CreateCheckoutSessionAsync(Payment payment, string customerEmail, string successUrl, string cancelUrl)
    {
        CreateCalls++;
        CreatedAttempts.Add(payment.CreatedAt.Ticks);
        if (CreateError is not null) throw CreateError;
        SuccessUrl = successUrl;
        return Task.FromResult(new StripeSessionInfo($"cs_test_{payment.PaymentId}", $"/Payments/Success?paymentId={payment.PaymentId}"));
    }
    public Task<OnlinePaymentConfirmation?> GetPaidConfirmationAsync(string sessionId) { LookupCalls++; return Task.FromResult<OnlinePaymentConfirmation?>(null); }
    public Task<bool> TryExpireSessionAsync(string sessionId) { ExpireCalls++; return Task.FromResult(true); }
    public Task<string> RefundAsync(string paymentIntentId, decimal amount, string idempotencyKey)
    {
        RefundCalls++;
        if (RefundError is not null) throw RefundError;
        return Task.FromResult("re_test_confirmed");
    }
    public Event ConstructWebhookEvent(string json, string signatureHeader) => VerifiedEvent ?? throw new NotSupportedException("Test gateway never accepts external callbacks");
}
