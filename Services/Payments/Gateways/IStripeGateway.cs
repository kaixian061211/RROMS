using RROMS.Web.Models.Payments;
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

    Task<OnlinePaymentConfirmation?> GetPaidConfirmationAsync(string sessionId);

    Task<bool> TryExpireSessionAsync(string sessionId);

    Task<string> RefundAsync(
        string paymentIntentId,
        decimal amount,
        string idempotencyKey);

    Event ConstructWebhookEvent(
        string json,
        string signatureHeader);
}