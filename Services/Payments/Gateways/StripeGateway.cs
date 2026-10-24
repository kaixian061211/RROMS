using System.Net;
using System.Globalization;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
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

        // Stripe accepts an expiry between 30 minutes and 24 hours.
        var expiryMinutes = Math.Clamp(
            _settings.CheckoutExpiryMinutes,
            31,
            1440);

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            AllowedPaymentMethodTypes = new List<string>
            {
                ToStripeMethodType(payment.PaymentMethod)
            },
            ClientReferenceId = payment.PaymentId.ToString(
                CultureInfo.InvariantCulture),
            CustomerEmail = string.IsNullOrWhiteSpace(customerEmail)
                ? null
                : customerEmail,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Metadata = new Dictionary<string, string>
            {
                ["payment_id"] = payment.PaymentId.ToString(
                    CultureInfo.InvariantCulture),
                ["order_id"] = payment.OrderId.ToString(
                    CultureInfo.InvariantCulture),
                ["attempt_ticks"] = payment.CreatedAt.Ticks.ToString(
                    CultureInfo.InvariantCulture)
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
                        ProductData =
                            new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"RROMS order #{payment.OrderId}"
                            }
                    }
                }
            }
        };

        // CreatedAt changes for each new payment attempt.
        // The key remains stable for retries of the same attempt.
        var requestOptions = new RequestOptions
        {
            IdempotencyKey =
                $"checkout-payment-{payment.PaymentId}-{payment.CreatedAt.Ticks}"
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

            bool definiteRejection =
                ex.HttpStatusCode == HttpStatusCode.BadRequest &&
                ex.StripeError?.Type == "invalid_request_error" &&
                ex.StripeError?.Code != "idempotency_key_in_use";

            throw new PaymentException(
                definiteRejection
                    ? "Stripe could not start checkout. Check the payment settings or choose another method."
                    : "Stripe has not confirmed this attempt. Please check your payment status before trying again.",
                paymentMayBeInProgress: !definiteRejection);
        }
        catch (Exception ex)
            when (ex is HttpRequestException or OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Checkout response was interrupted for payment {PaymentId}",
                payment.PaymentId);

            throw new PaymentException(
                "The connection was interrupted. Your payment status is not confirmed yet. Please check it before trying again.",
                paymentMayBeInProgress: true);
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
            // It may already be expired or already paid.
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

    public Task<string> RefundAsync(
        string paymentIntentId,
        decimal amount,
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new PaymentException(
                "The Stripe refund reference is missing.");
        }

        return ProcessRefundAsync(
            paymentIntentId,
            amount,
            idempotencyKey);
    }

    public Task<string> RecheckRefundAsync(
        string paymentIntentId,
        decimal amount) =>
        ProcessRefundAsync(
            paymentIntentId,
            amount,
            idempotencyKey: null);

    private async Task<string> ProcessRefundAsync(
        string paymentIntentId,
        decimal amount,
        string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            throw new PaymentException(
                "The Stripe refund reference is missing.");
        }

        if (amount <= 0 ||
            amount > 99999999.99m ||
            decimal.Round(amount, 2) != amount)
        {
            throw new PaymentException(
                "The refund amount is invalid.");
        }

        if (string.IsNullOrWhiteSpace(_settings.Currency) ||
            _settings.Currency.Trim().Length != 3)
        {
            throw new PaymentException(
                "The refund currency is not configured correctly.");
        }

        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            throw new PaymentException(
                "Stripe refunds are not configured. Add the Stripe test key before refunding an online payment.");
        }

        var expectedAmount = ToMinorUnits(amount);

        try
        {
            var service = new RefundService(GetClient());

            Refund? succeeded = null;
            Refund? active = null;
            Refund? failed = null;

            // Stripe lists newest first.
            // Check all pages so an older pending/succeeded refund is not missed.
            await foreach (
                var listed in service.ListAutoPagingAsync(
                    new RefundListOptions
                    {
                        PaymentIntent = paymentIntentId,
                        Limit = 100
                    }))
            {
                var refund = await GetVerifiedRefundAsync(
                    service,
                    listed.Id,
                    paymentIntentId,
                    expectedAmount);

                if (refund.Status == "succeeded")
                {
                    succeeded ??= refund;
                }
                else if (refund.Status is "failed" or "canceled")
                {
                    failed ??= refund;
                }
                else
                {
                    active ??= refund;
                }
            }

            var existing = succeeded ?? active ?? failed;

            if (existing is not null)
            {
                if (existing.Status == "succeeded")
                {
                    return existing.Id;
                }

                if (existing.Status is not ("failed" or "canceled") ||
                    idempotencyKey is null)
                {
                    return RequireSucceededRefund(existing);
                }

                // Concurrent clicks and interrupted retries use the same key.
                idempotencyKey += "-after-" + existing.Id;
            }
            else if (idempotencyKey is null)
            {
                throw new PaymentException(
                    "No Stripe refund was found for this payment. Use Refund / retry in full to request one.");
            }

            var created = await service.CreateAsync(
                new RefundCreateOptions
                {
                    PaymentIntent = paymentIntentId,
                    Amount = expectedAmount
                },
                new RequestOptions
                {
                    IdempotencyKey = idempotencyKey
                });

            // A cached idempotent response may contain an old pending status.
            var confirmed = await GetVerifiedRefundAsync(
                service,
                created.Id,
                paymentIntentId,
                expectedAmount);

            return RequireSucceededRefund(confirmed);
        }
        catch (StripeException ex)
        {
            _logger.LogError(
                ex,
                "Stripe refund could not be confirmed for {PaymentIntentId}",
                paymentIntentId);

            throw new PaymentException(
                "We could not confirm the refund with Stripe. Check the existing refund before submitting another request.",
                paymentMayBeInProgress: true);
        }
        catch (Exception ex)
            when (ex is HttpRequestException or OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Refund response was interrupted for {PaymentIntentId}",
                paymentIntentId);

            throw new PaymentException(
                "The connection was interrupted while checking the refund. Check Stripe before trying again.",
                paymentMayBeInProgress: true);
        }
    }

    private async Task<Refund> GetVerifiedRefundAsync(
        RefundService service,
        string refundId,
        string paymentIntentId,
        long expectedAmount)
    {
        if (string.IsNullOrWhiteSpace(refundId))
        {
            throw new PaymentException(
                "Stripe did not return a refund reference. Check Stripe before trying again.",
                true);
        }

        var refund = await service.GetAsync(refundId);

        if (refund.Id != refundId ||
            refund.PaymentIntentId != paymentIntentId ||
            refund.Amount != expectedAmount ||
            !string.Equals(
                refund.Currency,
                _settings.Currency.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Stripe refund {RefundId} did not match the expected payment details",
                refundId);

            throw new PaymentException(
                "The refund details do not match this payment. Check Stripe before trying again.",
                true);
        }

        _logger.LogInformation(
            "Stripe refund {RefundId} has status {Status}",
            refund.Id,
            refund.Status);

        return refund;
    }

    private static string RequireSucceededRefund(Refund refund) =>
        refund.Status switch
        {
            "succeeded" => refund.Id,

            "pending" or "requires_action" =>
                throw new PaymentException(
                    "The Stripe refund is awaiting confirmation. The payment remains marked paid. Use Recheck refund to check it again.",
                    paymentMayBeInProgress: true),

            "failed" or "canceled" =>
                throw new PaymentException(
                    "The Stripe refund failed or was cancelled. The payment remains marked paid. Use Refund / retry in full to request another attempt."),

            _ => throw new PaymentException(
                "Stripe has not confirmed the refund. Check its status in Stripe before trying again.",
                paymentMayBeInProgress: true)
        };

    public Event ConstructWebhookEvent(
        string json,
        string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_settings.WebhookSecret))
        {
            throw new InvalidOperationException(
                "Stripe:WebhookSecret is not configured.");
        }

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

    private static string ToStripeMethodType(string paymentMethod) =>
        paymentMethod switch
        {
            PaymentMethod.Card => "card",
            PaymentMethod.OnlineBanking => "fpx",
            PaymentMethod.EWallet => "grabpay",

            _ => throw new PaymentException(
                "That payment method can't be paid online.")
        };

    // Stripe wants whole sen: RM85.50 becomes 8550.
    private static long ToMinorUnits(decimal amount) =>
        (long)Math.Round(
            amount * 100m,
            MidpointRounding.AwayFromZero);
}