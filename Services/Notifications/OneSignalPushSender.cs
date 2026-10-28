using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RROMS.Web.Models.Notifications;

namespace RROMS.Web.Services.Notifications;

public sealed class OneSignalPushSender : INotificationPushSender
{
    private readonly HttpClient _http;
    private readonly WebPushSettings _settings;
    private readonly WebPushIdentity _identity;
    private readonly ILogger<OneSignalPushSender> _logger;

    public OneSignalPushSender(
        HttpClient http,
        IOptions<WebPushSettings> settings,
        WebPushIdentity identity,
        ILogger<OneSignalPushSender> logger)
    {
        _http = http;
        _settings = settings.Value;
        _identity = identity;
        _logger = logger;
    }

    public async Task SendAsync(
        Notification notification,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.NotificationId <= 0 || notification.UserId <= 0)
        {
            throw new ArgumentException(
                "A saved notification and owner are required.");
        }

        if (string.IsNullOrWhiteSpace(_settings.AppId)
            || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogInformation(
                "Web Push is not configured for notification {NotificationId}.",
                notification.NotificationId);

            return;
        }

        if (!Guid.TryParse(_settings.AppId, out var appId)
            || appId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The OneSignal App ID is invalid.");
        }

        if (!Uri.TryCreate(
                _settings.PublicBaseUrl.TrimEnd('/') + "/",
                UriKind.Absolute,
                out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(baseUri.UserInfo)
            || !string.IsNullOrEmpty(baseUri.Query)
            || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new InvalidOperationException(
                "Configure an HTTPS public base URL without credentials, query or fragment.");
        }

        var path = notification.OrderId is int orderId
            ? $"Tracking?orderId={orderId}"
            : notification.ReservationId is int reservationId
                ? $"Tracking?reservationId={reservationId}"
                : notification.PaymentId is int paymentId
                    ? $"Tracking?paymentId={paymentId}"
                    : "Notifications";

        var payload = new
        {
            app_id = appId.ToString("D"),
            include_aliases = new
            {
                external_id = new[]
                {
                    _identity.GetExternalId(notification.UserId)
                }
            },
            target_channel = "push",
            headings = new { en = notification.Title },
            contents = new { en = notification.Message },
            url = new Uri(baseUri, path).AbsoluteUri,
            idempotency_key = GetRequestKey(appId, notification)
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.onesignal.com/notifications");

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Key",
            _settings.ApiKey.Trim());

        request.Content = JsonContent.Create(payload);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(ct));

        if (!json.RootElement.TryGetProperty("id", out var id)
            || id.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(id.GetString()))
        {
            throw new InvalidOperationException(
                "OneSignal did not accept the push; the customer may have no active subscription.");
        }
    }

    private static string GetRequestKey(
        Guid appId,
        Notification notification)
    {
        var value = FormattableString.Invariant(
            $"{appId:D}:{notification.NotificationId}:{notification.CreatedAt.Ticks}");

        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(value))[..16];

        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);

        var hex = Convert.ToHexString(bytes).ToLowerInvariant();

        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }
}