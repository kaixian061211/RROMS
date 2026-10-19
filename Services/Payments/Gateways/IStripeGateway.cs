using Stripe;
using Payment = RROMS.Web.Models.Payments.Payment;

namespace RROMS.Web.Services.Payments.Gateways;

public record StripeSessionInfo(string SessionId, string Url);

/// <summary>Everything the app needs from Stripe, so the rest of the code never touches the Stripe SDK.</summary>
public interface IStripeGateway
{
    /// <summary>Creates a hosted Checkout page for one pending payment.</summary>
    Task<StripeSessionInfo> CreateCheckoutSessionAsync(Payment payment, string customerEmail, string successUrl, string cancelUrl);

    /// <summary>
    /// Closes an open Checkout page so it can no longer be paid.
    /// Returns true if the session is now expired, false if it could not be closed
    /// (usually because the customer has already paid).
    /// </summary>
    Task<bool> TryExpireSessionAsync(string sessionId);

    /// <summary>Refunds a PaymentIntent in full. Returns Stripe's refund id.</summary>
    Task<string> RefundAsync(string paymentIntentId, string idempotencyKey);

    /// <summary>Verifies the Stripe-Signature header and parses the webhook body.</summary>
    Event ConstructWebhookEvent(string json, string signatureHeader);
}
