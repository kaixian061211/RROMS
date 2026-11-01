using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;
using RROMS.Web.Services.Payments.Gateways;
using Stripe;
using Payment = RROMS.Web.Models.Payments.Payment;
using PaymentMethod = RROMS.Web.Models.Payments.PaymentMethod;

namespace RROMS.Tests.Payments;

[Trait("Type", "Unit")]
public class StripeGatewayTests
{
    [Theory]
    [InlineData(PaymentMethod.Card, "card")]
    [InlineData(PaymentMethod.OnlineBanking, "fpx")]
    [InlineData(PaymentMethod.EWallet, "grabpay")]
    public async Task CheckoutSendsWholeSenAndAStableAttemptKey(string method, string stripeMethod)
    {
        var http = new ScriptedStripe(_ => Json("{\"id\":\"cs_test_gateway\",\"object\":\"checkout.session\",\"url\":\"https://checkout.stripe.test/session\"}"));
        var gateway = Gateway(http);
        var payment = new Payment { PaymentId = 7, OrderId = 12, Amount = 85.50m, PaymentMethod = method, CreatedAt = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc) };
        await gateway.CreateCheckoutSessionAsync(payment, "customer@example.test", "https://localhost/success", "https://localhost/cancel");
        var sent = Assert.Single(http.Requests);
        var body = Uri.UnescapeDataString(sent.Body);
        Assert.Contains("unit_amount]=8550", body);
        Assert.Contains($"allowed_payment_method_types[0]={stripeMethod}", body);
        Assert.Contains("currency]=myr", body);
        Assert.Contains("branding_settings[display_name]=RROMS", body);
        Assert.Contains("branding_settings[button_color]=#0B6B4F", body);
        Assert.Contains("branding_settings[background_color]=#F1F3F1", body);
        Assert.Contains("branding_settings[border_style]=rounded", body);
        Assert.Contains("branding_settings[font_family]=inter", body);
        var expiry = new DateTimeOffset(payment.CreatedAt.AddMinutes(35)).ToUnixTimeSeconds();
        Assert.Contains($"expires_at={expiry}", body);
        Assert.Equal($"checkout-payment-7-{payment.CreatedAt.Ticks}", sent.Key);
    }

    [Fact]
    public async Task NonMyrConfigurationIsRejectedBeforeAnyNetworkCall()
    {
        var http = new ScriptedStripe(_ => throw new InvalidOperationException("Must not send"));
        var gateway = Gateway(http, "USD");
        await Assert.ThrowsAsync<PaymentException>(() => gateway.CreateCheckoutSessionAsync(new Payment { PaymentMethod = PaymentMethod.Card, Amount = 1m }, "", "https://localhost/s", "https://localhost/c"));
        await Assert.ThrowsAsync<PaymentException>(() => gateway.RefundAsync("pi_test", 1m, "refund-test"));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task InterruptedSessionClosureCannotPermitAnotherPayment()
    {
        var http = new ScriptedStripe(_ => throw new HttpRequestException("Simulated offline connection"));
        Assert.False(await Gateway(http).TryExpireSessionAsync("cs_test_gateway"));
        Assert.Equal(2, http.Requests.Count);
    }

    [Fact]
    public async Task AnInterruptedClosureCanBeRecoveredByVerifiedExpiry()
    {
        var http = new ScriptedStripe(request => request.Method == HttpMethod.Post
            ? throw new HttpRequestException("Simulated interrupted response")
            : Json("{\"id\":\"cs_test_gateway\",\"object\":\"checkout.session\",\"status\":\"expired\"}"));
        Assert.True(await Gateway(http).TryExpireSessionAsync("cs_test_gateway"));
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("requires_action")]
    public async Task AnUnconfirmedRefundCannotBeReportedAsSucceededOrCreatedAgain(string status)
    {
        var http = ExistingRefund(status);
        var error = await Assert.ThrowsAsync<PaymentException>(() => Gateway(http).RefundAsync("pi_test_gateway", 85.50m, "refund-payment-7"));
        Assert.True(error.PaymentMayBeInProgress);
        Assert.DoesNotContain("automatically", error.Message);
        Assert.All(http.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Fact]
    public async Task APreviouslySucceededRefundIsReusedWithoutMovingMoneyAgain()
    {
        var http = ExistingRefund("succeeded");
        Assert.Equal("re_test_gateway", await Gateway(http).RefundAsync("pi_test_gateway", 85.50m, "refund-payment-7"));
        Assert.All(http.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Theory]
    [InlineData("currency")]
    [InlineData("amount")]
    [InlineData("intent")]
    public async Task InconsistentRefundReferencesCannotChangeTheLocalResult(string mismatch)
    {
        var body = Refund("succeeded");
        body = mismatch switch { "currency" => body.Replace("myr", "usd"), "amount" => body.Replace("8550", "8551"), _ => body.Replace("pi_test_gateway", "pi_test_other") };
        var http = new ScriptedStripe(request => Json(request.RequestUri!.AbsolutePath == "/v1/refunds" ? RefundList : body));
        await Assert.ThrowsAsync<PaymentException>(() => Gateway(http).RefundAsync("pi_test_gateway", 85.50m, "refund-payment-7"));
        Assert.All(http.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Fact]
    public async Task AConfirmedFailedRefundUsesOneStableReplacementKey()
    {
        var http = new ScriptedStripe(request => Json(
            request.Method == HttpMethod.Post ? Refund("succeeded", "re_test_replacement") :
            request.RequestUri!.AbsolutePath == "/v1/refunds" ? RefundList :
            request.RequestUri.AbsolutePath.EndsWith("re_test_gateway") ? Refund("failed") : Refund("succeeded", "re_test_replacement")));
        Assert.Equal("re_test_replacement", await Gateway(http).RefundAsync("pi_test_gateway", 85.50m, "refund-payment-7"));
        var created = Assert.Single(http.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal("refund-payment-7-after-re_test_gateway", created.Key);
        Assert.Contains("amount=8550", created.Body);
    }

    [Theory]
    [InlineData("checkout.session.completed")]
    [InlineData("checkout.session.async_payment_succeeded")]
    public async Task PaidLookupUsesStripeSuccessEventTimeInsteadOfCheckoutStartOrReturn(string type)
    {
        var http = new ScriptedStripe(request => Json(request.RequestUri!.AbsolutePath == "/v1/events"
            ? SuccessEvents(type) : PaidCheckout));
        var confirmation = Assert.IsType<OnlinePaymentConfirmation>(await Gateway(http).GetPaidConfirmationAsync("cs_test_gateway"));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793289540).UtcDateTime, confirmation.PaidAtUtc);
        Assert.Equal(2, http.Requests.Count);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("other-session")]
    [InlineData("still-processing")]
    [InlineData("wrong-amount")]
    [InlineData("future")]
    public async Task PaidLookupCannotInventASuccessDateWhenEventIsUnverifiable(string problem)
    {
        var events = SuccessEvents("checkout.session.completed");
        events = problem switch
        {
            "missing" => "{\"object\":\"list\",\"data\":[],\"has_more\":false}",
            "other-session" => events.Replace("cs_test_gateway", "cs_test_other"),
            "still-processing" => events.Replace("\"paid\"", "\"unpaid\""),
            "wrong-amount" => events.Replace("8550", "8551"),
            _ => events.Replace("1793289540", "4102444800")
        };
        var http = new ScriptedStripe(request => Json(request.RequestUri!.AbsolutePath == "/v1/events" ? events : PaidCheckout));
        var error = await Assert.ThrowsAsync<PaymentException>(() => Gateway(http).GetPaidConfirmationAsync("cs_test_gateway"));
        Assert.True(error.PaymentMayBeInProgress);
        Assert.All(http.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Fact]
    public async Task UnpaidLookupDoesNotScanSuccessEvents()
    {
        var http = new ScriptedStripe(_ => Json(PaidCheckout.Replace("\"paid\"", "\"unpaid\"")));
        Assert.Null(await Gateway(http).GetPaidConfirmationAsync("cs_test_gateway"));
        Assert.Single(http.Requests);
    }

    private const string PaidCheckout = """{"id":"cs_test_gateway","object":"checkout.session","created":1793286000,"mode":"payment","payment_status":"paid","amount_total":8550,"currency":"myr","client_reference_id":"7","payment_intent":"pi_test_gateway","metadata":{"payment_id":"7","order_id":"12","attempt_ticks":"123"}}""";
    private static string SuccessEvents(string type) =>
        $$$"""{"object":"list","has_more":false,"data":[{"id":"evt_test_success","object":"event","created":1793289540,"type":"{{{type}}}","data":{"object":{{{PaidCheckout}}}}}]}""";

    private const string RefundList = "{\"object\":\"list\",\"data\":[{\"id\":\"re_test_gateway\",\"object\":\"refund\"}],\"has_more\":false}";
    private static string Refund(string status, string id = "re_test_gateway") =>
        $$"""{"id":"{{id}}","object":"refund","payment_intent":"pi_test_gateway","amount":8550,"currency":"myr","status":"{{status}}"}""";
    private static ScriptedStripe ExistingRefund(string status) => new(request =>
        Json(request.RequestUri!.AbsolutePath == "/v1/refunds" ? RefundList : Refund(status)));
    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    private static StripeGateway Gateway(ScriptedStripe http, string currency = "MYR") => new(
        Options.Create(new StripeSettings { SecretKey = "sk_test_in_process_only", Currency = currency }),
        NullLogger<StripeGateway>.Instance,
        new StripeClient("sk_test_in_process_only", httpClient: new SystemNetHttpClient(new HttpClient(http), maxNetworkRetries: 0, enableTelemetry: false)));

    private sealed class ScriptedStripe(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Body, string? Key)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, body, request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null));
            return respond(request);
        }
    }
}
