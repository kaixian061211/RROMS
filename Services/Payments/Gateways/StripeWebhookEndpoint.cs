namespace RROMS.Web.Services.Payments.Gateways;

public static class StripeWebhookEndpoint
{
    /// <summary>
    /// POST /webhooks/stripe - called by Stripe, so it is anonymous. Security comes from the
    /// signature check inside <see cref="StripeWebhookHandler"/>.
    /// This is a minimal-API endpoint (not an MVC controller) because Stripe posts raw JSON, not a page form.
    /// </summary>
    public static IEndpointRouteBuilder MapStripeWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/stripe", async (HttpRequest request, StripeWebhookHandler handler) =>
        {
            // The signature is calculated over the exact raw body, so read it untouched.
            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync();
            var signature = request.Headers["Stripe-Signature"].ToString();

            var outcome = await handler.HandleAsync(json, signature);

            // 4xx tells Stripe the request was bad; 2xx means "got it, stop retrying".
            return outcome == WebhookOutcome.InvalidSignature ? Results.BadRequest() : Results.Ok();
        }).AllowAnonymous();

        return app;
    }
}