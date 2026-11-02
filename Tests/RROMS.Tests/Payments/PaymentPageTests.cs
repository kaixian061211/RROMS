using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RROMS.Tests.Integration;
using RROMS.Web.Models.Dev;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments.Gateways;
using RROMS.Web.Services.Payments.Notifications;

namespace RROMS.Tests.Payments;

public class PaymentWebApp : TestWebApp
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Stripe:SecretKey", "");
        builder.UseSetting("Smtp:Host", "");
        builder.UseSetting("Notifications:WebPush:AppId", "");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IStripeGateway>();
            services.AddSingleton<IStripeGateway, PaymentTestStripe>();
            services.RemoveAll<IPaymentNotifier>();
        });
    }
}

[Trait("Type", "Integration")]
public class PaymentPageTests(PaymentWebApp app) : PageTest(app), IClassFixture<PaymentWebApp>
{
    [Fact]
    public async Task CheckoutIgnoresClientSuppliedAmountAndDuplicateCounterPostsKeepOneRow()
    {
        var order = await AddOrderAsync();
        var page = await GetPageAsync($"/Payments/Checkout?orderId={order.OrderId}");
        var form = page.Forms.Single(f => f.QuerySelector("input[name=OrderId]") is not null);
        var result = await SubmitAsync(form, new() { ["Method"] = PaymentMethod.PayAtCounter, ["Amount"] = "0.01" });
        Assert.Contains("Pay at the counter", TextOf(result));
        await SubmitAsync(form, new() { ["Method"] = PaymentMethod.PayAtCounter });
        var payment = await WithDatabaseAsync(db => db.Payments.SingleAsync(p => p.OrderId == order.OrderId));
        Assert.Equal(25.80m, payment.Amount);
        Assert.Equal(PaymentStatus.Pending, payment.PaymentStatus);
        Assert.InRange(payment.CreatedAt, DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task InvalidMethodKeepsCheckoutOpenWithoutCreatingAPayment()
    {
        var order = await AddOrderAsync();
        var page = await GetPageAsync($"/Payments/Checkout?orderId={order.OrderId}");
        var form = page.Forms.Single(f => f.QuerySelector("input[name=OrderId]") is not null);
        var result = await SubmitAsync(form, new() { ["Method"] = "unsupported" });
        Assert.Contains("Choose a supported payment method", TextOf(result));
        Assert.False(await WithDatabaseAsync(db => db.Payments.AnyAsync()));
    }

    [Fact]
    public async Task CustomerCannotReadAnotherCustomersCheckoutStatusOrReceipt()
    {
        var order = await AddOrderAsync();
        var payment = await AddPaymentAsync(order.OrderId, PaymentStatus.Paid);
        await SignInAsAsync(TestUsers.CustomerB);
        foreach (var path in new[] { $"/Payments/Checkout?orderId={order.OrderId}", $"/Payments/Success?paymentId={payment.PaymentId}", $"/Payments/Success?handler=Status&paymentId={payment.PaymentId}", $"/Payments/Receipt/{payment.PaymentId}" })
        {
            var response = await Client.GetAsync(path);
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden
                || response.RequestMessage!.RequestUri!.AbsolutePath == "/Account/AccessDenied", $"Unexpected response for {path}: {response.StatusCode}");
        }
        var history = await GetPageAsync("/Payments/History");
        Assert.DoesNotContain(payment.ReceiptNo(), TextOf(history));
    }

    [Fact]
    public async Task AdminCanViewButCannotRefundEvenWithAForgedForm()
    {
        var order = await AddOrderAsync();
        var payment = await AddPaymentAsync(order.OrderId, PaymentStatus.Paid);
        await SignInAsAsync(TestUsers.Admin);
        var details = await GetPageAsync($"/Payments/Details/{payment.PaymentId}");
        Assert.DoesNotContain(details.Forms, form => form.Action.Contains("handler=Refund"));
        var token = details.QuerySelector<IHtmlInputElement>("input[name=__RequestVerificationToken]")!.Value;
        var response = await Client.PostAsync($"/Payments/Details/{payment.PaymentId}?handler=Refund", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal("/Account/AccessDenied", response.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Equal(PaymentStatus.Paid, await WithDatabaseAsync(db => db.Payments.Where(p => p.PaymentId == payment.PaymentId).Select(p => p.PaymentStatus).SingleAsync()));
    }

    [Fact]
    public async Task StaffCanRecordAFullCounterRefundThroughThePage()
    {
        var order = await AddOrderAsync();
        var payment = await AddPaymentAsync(order.OrderId, PaymentStatus.Paid);
        await SignInAsAsync(TestUsers.Staff);
        var details = await GetPageAsync($"/Payments/Details/{payment.PaymentId}");
        var form = details.Forms.Single(f => f.Action.Contains("handler=Refund"));
        var refunded = await SubmitAsync(form, new() { ["reason"] = "Returned at counter" });
        Assert.Contains("Refunded", TextOf(refunded));
        var saved = await WithDatabaseAsync(db => db.Payments.AsNoTracking().SingleAsync(p => p.PaymentId == payment.PaymentId));
        Assert.Equal(PaymentStatus.Refunded, saved.PaymentStatus);
        Assert.Equal(TestUsers.Staff.Id, saved.RefundedByStaffId);
        Assert.Equal("Returned at counter", saved.RefundReason);
        var receipt = await Client.GetAsync($"/Payments/Receipt/{payment.PaymentId}");
        Assert.Equal("application/pdf", receipt.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString((await receipt.Content.ReadAsByteArrayAsync())[..4]));
    }

    [Fact]
    public async Task CheckoutForAPaidOrderShowsTheConfirmedOutcomeInsteadOfAnotherForm()
    {
        var order = await AddOrderAsync();
        var payment = await AddPaymentAsync(order.OrderId, PaymentStatus.Paid);
        var page = await GetPageAsync($"/Payments/Checkout?orderId={order.OrderId}");
        Assert.Contains("Payment received", TextOf(page));
        Assert.Contains(page.QuerySelectorAll("a"), link => link.GetAttribute("href") == $"/Payments/Receipt/{payment.PaymentId}");
        Assert.Null(page.QuerySelector("input[name=Method]"));
    }

    [Fact]
    public async Task StatusPollingReadsTheSavedRowWithoutContactingStripe()
    {
        var order = await AddOrderAsync();
        var payment = await AddPaymentAsync(order.OrderId, PaymentStatus.Pending);
        await WithDatabaseAsync(db => db.Payments.Where(p => p.PaymentId == payment.PaymentId).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.PaymentMethod, PaymentMethod.Card).SetProperty(p => p.RecordedByStaffId, (int?)null)
            .SetProperty(p => p.TransactionRef, "cs_test_polling")));
        var stripe = (PaymentTestStripe)app.Services.GetRequiredService<IStripeGateway>();
        var lookups = stripe.LookupCalls;
        var response = await Client.GetAsync($"/Payments/Success?handler=Status&paymentId={payment.PaymentId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("pending", await response.Content.ReadAsStringAsync());
        Assert.Equal(lookups, stripe.LookupCalls);
    }

    [Fact]
    public async Task MissingSessionReferenceOffersResumeWithoutAnEndlessPoll()
    {
        var order = await AddOrderAsync();
        var payment = await AddPaymentAsync(order.OrderId, PaymentStatus.Pending);
        await WithDatabaseAsync(db => db.Payments.Where(p => p.PaymentId == payment.PaymentId).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.PaymentMethod, PaymentMethod.Card).SetProperty(p => p.RecordedByStaffId, (int?)null)));
        var page = await GetPageAsync($"/Payments/Success?paymentId={payment.PaymentId}");
        Assert.Contains("Resume previous checkout", TextOf(page));
        Assert.Null(page.QuerySelector("#poll"));
        Assert.DoesNotContain(page.QuerySelectorAll("script"), script => script.GetAttribute("src")?.Contains("payments-status.js") == true);
    }

    [Fact]
    public async Task PaymentPostsRequireAnAntiforgeryToken()
    {
        var order = await AddOrderAsync();
        var response = await Client.PostAsync($"/Payments/Checkout?orderId={order.OrderId}", new FormUrlEncodedContent(new Dictionary<string,string> { ["OrderId"] = order.OrderId.ToString(), ["Method"] = PaymentMethod.PayAtCounter }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await WithDatabaseAsync(db => db.Payments.AnyAsync()));
    }

    private Task<Order> AddOrderAsync() => WithDatabaseAsync(async db =>
    {
        var order = new Order { CustomerUserId = TestCustomer, TableId = 4, Items = { new OrderItem { MenuItemId = NasiLemak, Quantity = 2, UnitPrice = 12.90m } } };
        db.Add(order);
        await db.SaveChangesAsync();
        return order;
    });
    private Task<Payment> AddPaymentAsync(int orderId, string status) => WithDatabaseAsync(async db =>
    {
        var payment = new Payment { OrderId = orderId, Amount = 25.80m, PaymentStatus = status, PaymentMethod = PaymentMethod.Cash, RecordedByStaffId = TestUsers.Staff.Id, PaidAt = status == PaymentStatus.Paid ? DateTime.UtcNow : null };
        db.Add(payment);
        await db.SaveChangesAsync();
        return payment;
    });
}

internal static class PaymentTestReferences
{
    internal static string ReceiptNo(this Payment payment) => $"RCP-{payment.PaymentId:D6}";
}
