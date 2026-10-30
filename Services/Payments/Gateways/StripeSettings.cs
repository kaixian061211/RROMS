namespace RROMS.Web.Services.Payments.Gateways;

/// <summary>Bound from the "Stripe" section. Supply keys through environment variables.</summary>
public class StripeSettings
{
    public const string SectionName = "Stripe";

    /// <summary>Stripe__SecretKey: sk_test_... from the Stripe Dashboard.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Stripe__WebhookSecret: signing secret for an optional webhook endpoint.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    public string Currency { get; set; } = "myr";

    /// <summary>How long a Checkout page stays open. Stripe allows 30 minutes to 24 hours.</summary>
    public int CheckoutExpiryMinutes { get; set; } = 35;
}
