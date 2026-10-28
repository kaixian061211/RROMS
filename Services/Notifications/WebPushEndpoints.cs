using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Services.Notifications;

public static class WebPushEndpoints
{
    public static IEndpointRouteBuilder MapNotificationWebPush(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/notifications/push-token",
            (HttpContext context, IAntiforgery antiforgery) =>
            {
                context.Response.Headers.CacheControl = "no-store";

                var tokens = antiforgery.GetAndStoreTokens(context);

                return Results.Json(new
                {
                    formFieldName = tokens.FormFieldName,
                    requestToken = tokens.RequestToken
                });
            })
            .AllowAnonymous();

        endpoints.MapPost(
            "/notifications/push-identity",
            async (
                HttpContext context,
                IAntiforgery antiforgery,
                IOptions<WebPushSettings> settings,
                WebPushIdentity identity) =>
            {
                context.Response.Headers.CacheControl = "no-store";

                try
                {
                    await antiforgery.ValidateRequestAsync(context);
                }
                catch (AntiforgeryValidationException)
                {
                    return Results.BadRequest();
                }

                if (!context.User.TryGetPaymentUserId(out var userId)
                    || userId <= 0)
                {
                    return Results.StatusCode(
                        StatusCodes.Status403Forbidden);
                }

                if (!Guid.TryParse(settings.Value.AppId, out var appId)
                    || appId == Guid.Empty
                    || settings.Value.AliasSecret.Length < 32)
                {
                    return Results.Problem(
                        "Browser notifications are not configured.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }

                return Results.Json(new
                {
                    externalId = identity.GetExternalId(userId)
                });
            })
            .RequireAuthorization(
                policy => policy.RequireRole(PaymentRoles.Customer));

        return endpoints;
    }
}