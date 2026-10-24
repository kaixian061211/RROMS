using Stripe;
using Payment = RROMS.Web.Models.Payments.Payment;

namespace RROMS.Web.Services.Payments.Gateways;

public record StripeSessionInfo(
    string SessionId,
    string Url,
    string? PaymentIntentId = null);

public interface IStripeGateway
{
    Task<StripeSessionInfo> CreateCheckoutSessionAsync(
        Payment payment,
        string customerEmail,
        string successUrl,
        string cancelUrl);

    Task<bool> TryExpireSessionAsync(string sessionId);

    Task<string> RefundAsync(
        string paymentIntentId,
        decimal amount,
        string idempotencyKey);

    /// <summary>
    /// Checks an existing Stripe refund without creating another refund.
    /// </summary>
    Task<string> RecheckRefundAsync(
        string paymentIntentId,
        decimal amount);

    Event ConstructWebhookEvent(
        string json,
        string signatureHeader);
}