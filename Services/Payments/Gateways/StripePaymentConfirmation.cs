using System.Globalization;
using RROMS.Web.Models.Payments;
using Stripe.Checkout;

namespace RROMS.Web.Services.Payments.Gateways;

/// <summary>
/// Reads payment confirmation details from trusted Stripe session data.
/// PaymentService validates them against the stored payment.
/// </summary>
internal static class StripePaymentConfirmation
{
    public static bool TryCreate(
        Session session,
        out OnlinePaymentConfirmation? confirmation)
    {
        confirmation = null;

        if (session.Mode != "payment"
            || session.PaymentStatus != "paid"
            || string.IsNullOrWhiteSpace(session.Id)
            || session.Id.Length > 100
            || session.AmountTotal is not long amountTotal
            || amountTotal <= 0
            || string.IsNullOrWhiteSpace(session.Currency)
            || session.PaymentIntentId is { Length: > 100 }
            || !int.TryParse(
                session.ClientReferenceId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var paymentId)
            || paymentId <= 0
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
