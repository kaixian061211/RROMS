using Stripe;
using Stripe.Checkout;

namespace RROMS.Web.Services.Payments.Gateways;

public enum WebhookOutcome
{
    Processed,
    Ignored,
    InvalidSignature
}

/// <summary>
/// Turns Stripe's events into payment status changes. This - not the browser redirect - is the
/// source of truth for "was the customer charged?".
/// </summary>
public class StripeWebhookHandler
{
    private readonly IStripeGateway _stripe;
    private readonly IPaymentService _payments;
    private readonly ILogger<StripeWebhookHandler> _logger;

    public StripeWebhookHandler(IStripeGateway stripe, IPaymentService payments, ILogger<StripeWebhookHandler> logger)
    {
        _stripe = stripe;
        _payments = payments;
        _logger = logger;
    }

    public async Task<WebhookOutcome> HandleAsync(string json, string signatureHeader)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = _stripe.ConstructWebhookEvent(json, signatureHeader);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Rejected a Stripe webhook with an invalid signature");
            return WebhookOutcome.InvalidSignature;
        }

        // We only care about Checkout Sessions; everything else is acknowledged and ignored.
        if (stripeEvent.Data.Object is not Session session)
        {
            return WebhookOutcome.Ignored;
        }

        // We set client_reference_id to our payment id when we created the session.
        if (!int.TryParse(session.ClientReferenceId, out var paymentId))
        {
            _logger.LogWarning("Stripe event {EventId} has no usable payment reference", stripeEvent.Id);
            return WebhookOutcome.Ignored;
        }

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
            case "checkout.session.async_payment_succeeded":
                // "completed" can arrive before the money does (delayed methods); only trust "paid".
                if (session.PaymentStatus == "paid")
                {
                    await _payments.MarkPaidOnlineAsync(paymentId, session.PaymentIntentId);
                }
                return WebhookOutcome.Processed;

            case "checkout.session.async_payment_failed":
            case "checkout.session.expired":
                // A payment has one row for all its attempts. Passing this event's session id makes sure an
                // old attempt's late "expired" event can't fail a newer attempt.
                await _payments.MarkFailedAsync(paymentId, session.Id);
                return WebhookOutcome.Processed;

            default:
                return WebhookOutcome.Ignored;
        }
    }
}