namespace RROMS.Web.Services.Payments.Gateways;

/// <summary>Bound from the "Stripe" section. Keys belong in user-secrets, never in Git.</summary>
public class StripeSettings
{
    public const string SectionName = "Stripe";

    /// <summary>sk_test_... from the Stripe Dashboard (Developers > API keys).</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>whsec_... printed by "stripe listen" (local) or the Dashboard webhook (deployed).</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    public string Currency { get; set; } = "myr";

    /// <summary>How long a Checkout page stays open. Stripe allows 30 minutes to 24 hours.</summary>
    public int CheckoutExpiryMinutes { get; set; } = 35;
}
