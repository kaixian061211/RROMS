using System.Globalization;
using RROMS.Web.Models.Payments;
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
/// Turns Stripe events into payment status changes.
/// The webhook is the source of truth for online payment confirmation.
/// </summary>
public class StripeWebhookHandler
{
    private readonly IStripeGateway _stripe;
    private readonly IPaymentService _payments;
    private readonly ILogger<StripeWebhookHandler> _logger;

    public StripeWebhookHandler(
        IStripeGateway stripe,
        IPaymentService payments,
        ILogger<StripeWebhookHandler> logger)
    {
        _stripe = stripe;
        _payments = payments;
        _logger = logger;
    }

    public async Task<WebhookOutcome> HandleAsync(
        string json,
        string signatureHeader)
    {
        Event stripeEvent;

        try
        {
            stripeEvent = _stripe.ConstructWebhookEvent(
                json,
                signatureHeader);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(
                ex,
                "Rejected a Stripe webhook with an invalid signature");

            return WebhookOutcome.InvalidSignature;
        }

        if (stripeEvent.Data.Object is not Session session)
        {
            return WebhookOutcome.Ignored;
        }

        if (!int.TryParse(
                session.ClientReferenceId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var paymentId)
            || paymentId <= 0
            || string.IsNullOrWhiteSpace(session.Id)
            || session.Id.Length > 100)
        {
            _logger.LogWarning(
                "Stripe event {EventId} has no usable payment reference",
                stripeEvent.Id);

            return WebhookOutcome.Ignored;
        }

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
            case "checkout.session.async_payment_succeeded":
                // Completion can arrive before a delayed payment succeeds.
                if (session.PaymentStatus == "paid")
                {
                    if (!TryGetConfirmation(
                            session,
                            paymentId,
                            out var confirmation))
                    {
                        return WebhookOutcome.Ignored;
                    }

                    await _payments.MarkPaidOnlineAsync(confirmation!);
                }

                return WebhookOutcome.Processed;

            case "checkout.session.async_payment_failed":
            case "checkout.session.expired":
                await _payments.MarkFailedAsync(
                    paymentId,
                    session.Id);

                return WebhookOutcome.Processed;

            default:
                return WebhookOutcome.Ignored;
        }
    }

    private bool TryGetConfirmation(
        Session session,
        int paymentId,
        out OnlinePaymentConfirmation? confirmation)
    {
        confirmation = null;

        if (session.Mode != "payment"
            || session.AmountTotal is not long amountTotal
            || string.IsNullOrWhiteSpace(session.Currency)
            || session.Metadata is null
            || !session.Metadata.TryGetValue(
                "payment_id",
                out var paymentReference)
            || !int.TryParse(
                paymentReference,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var metadataPaymentId)
            || metadataPaymentId != paymentId
            || !session.Metadata.TryGetValue(
                "order_id",
                out var orderReference)
            || !int.TryParse(
                orderReference,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var orderId)
            || orderId <= 0)
        {
            _logger.LogWarning(
                "Stripe session {SessionId} has incomplete or inconsistent payment details",
                session.Id);

            return false;
        }

        long? attemptTicks = null;

        if (session.Metadata.TryGetValue(
                "attempt_ticks",
                out var attemptReference))
        {
            if (!long.TryParse(
                    attemptReference,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var parsedTicks)
                || parsedTicks <= 0
                || parsedTicks > DateTime.MaxValue.Ticks)
            {
                _logger.LogWarning(
                    "Stripe session {SessionId} has an invalid checkout attempt",
                    session.Id);

                return false;
            }

            attemptTicks = parsedTicks;
        }

        confirmation = new OnlinePaymentConfirmation(
            paymentId,
            orderId,
            session.Id,
            session.PaymentIntentId,
            amountTotal,
            session.Currency,
            attemptTicks);

        return true;
    }
}