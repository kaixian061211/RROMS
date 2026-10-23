using System.Globalization;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
// Stripe's SDK also has a PaymentMethod class, so say which one we mean.
using Payment = RROMS.Web.Models.Payments.Payment;
using PaymentMethod = RROMS.Web.Models.Payments.PaymentMethod;

namespace RROMS.Web.Services.Payments.Gateways;

public class StripeGateway : IStripeGateway
{
    private readonly StripeSettings _settings;
    private readonly ILogger<StripeGateway> _logger;
    private StripeClient? _client;

    public StripeGateway(
        IOptions<StripeSettings> options,
        ILogger<StripeGateway> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<StripeSessionInfo> CreateCheckoutSessionAsync(
        Payment payment,
        string customerEmail,
        string successUrl,
        string cancelUrl)
    {
        var client = GetClient();

        // Stripe only accepts an expiry between 30 minutes and 24 hours from the moment it
        // receives the request, so stay safely inside that window.
        var expiryMinutes = Math.Clamp(_settings.CheckoutExpiryMinutes, 31, 1440);

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            AllowedPaymentMethodTypes = new List<string>
            {
                ToStripeMethodType(payment.PaymentMethod)
            },
            ClientReferenceId = payment.PaymentId.ToString(CultureInfo.InvariantCulture),
            CustomerEmail = string.IsNullOrWhiteSpace(customerEmail)
                ? null
                : customerEmail,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Metadata = new Dictionary<string, string>
            {
                ["payment_id"] = payment.PaymentId.ToString(CultureInfo.InvariantCulture),
                ["order_id"] = payment.OrderId.ToString(CultureInfo.InvariantCulture),
                ["attempt_ticks"] = payment.CreatedAt.Ticks.ToString(CultureInfo.InvariantCulture)
            },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = _settings.Currency,
                        UnitAmount = ToMinorUnits(payment.Amount),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"RROMS order #{payment.OrderId}"
                        }
                    }
                }
            }
        };

        // A payment has ONE row for all its attempts, and CreatedAt is reset by every new attempt,
        // so it makes the key unique per attempt while still protecting against network retries.
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = $"checkout-payment-{payment.PaymentId}-{payment.CreatedAt.Ticks}"
        };

        try
        {
            var session = await new SessionService(client).CreateAsync(
                options,
                requestOptions);

            return new StripeSessionInfo(
                session.Id,
                session.Url,
                session.PaymentIntentId);
        }
        catch (StripeException ex)
        {
            _logger.LogError(
                ex,
                "Stripe could not create a Checkout Session for payment {PaymentId}",
                payment.PaymentId);

            throw new PaymentException(
                "We couldn't start the online payment. Please try again or choose another method.");
        }
    }

    public async Task<bool> TryExpireSessionAsync(string sessionId)
    {
        var service = new SessionService(GetClient());

        try
        {
            await service.ExpireAsync(sessionId);
            return true;
        }
        catch (StripeException)
        {
            // Expire fails if the session is already expired or already paid. Ask Stripe which one.
            try
            {
                var session = await service.GetAsync(sessionId);
                return session.Status == "expired";
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not check Checkout Session {SessionId}",
                    sessionId);

                return false;
            }
        }
    }

    public async Task<string> RefundAsync(
        string paymentIntentId,
        string idempotencyKey)
    {
        var options = new RefundCreateOptions
        {
            PaymentIntent = paymentIntentId
        };

        var requestOptions = new RequestOptions
        {
            IdempotencyKey = idempotencyKey
        };

        try
        {
            var refund = await new RefundService(GetClient()).CreateAsync(
                options,
                requestOptions);

            return refund.Id;
        }
        catch (StripeException ex)
        {
            _logger.LogError(
                ex,
                "Stripe refused the refund for {PaymentIntentId}",
                paymentIntentId);

            throw new PaymentException(
                $"Stripe could not process the refund: {ex.StripeError?.Message ?? ex.Message}");
        }
    }

    public Event ConstructWebhookEvent(string json, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_settings.WebhookSecret))
        {
            throw new InvalidOperationException(
                "Stripe:WebhookSecret is not configured.");
        }

        // throwOnApiVersionMismatch: false -> the SDK's pinned API version may differ from the
        // one your Stripe account uses; we only read fields that exist in both.
        return EventUtility.ConstructEvent(
            json,
            signatureHeader,
            _settings.WebhookSecret,
            throwOnApiVersionMismatch: false);
    }

    private StripeClient GetClient()
    {
        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            throw new PaymentException(
                "Online payments are not set up yet. Add the Stripe test keys (Stripe:SecretKey in user-secrets) or pay at the counter.");
        }

        return _client ??= new StripeClient(_settings.SecretKey);
    }

    /// <summary>Maps our PaymentMethod values onto Stripe's payment method types.</summary>
    private static string ToStripeMethodType(string paymentMethod) => paymentMethod switch
    {
        PaymentMethod.Card => "card",
        PaymentMethod.OnlineBanking => "fpx",
        PaymentMethod.EWallet => "grabpay",
        _ => throw new PaymentException("That payment method can't be paid online.")
    };

    /// <summary>Stripe wants whole sen: RM 85.50 becomes 8550.</summary>
    private static long ToMinorUnits(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
}