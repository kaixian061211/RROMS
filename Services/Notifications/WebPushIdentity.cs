using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace RROMS.Web.Services.Notifications;

public sealed class WebPushIdentity
{
    private readonly WebPushSettings _settings;

    public WebPushIdentity(IOptions<WebPushSettings> settings)
    {
        _settings = settings.Value;
    }

    public string GetExternalId(int userId)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));

        if (_settings.AliasSecret.Length < 32)
        {
            throw new InvalidOperationException(
                "Configure a random push alias secret of at least 32 characters.");
        }

        var value =
            "rroms:customer:" + userId.ToString(CultureInfo.InvariantCulture);

        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_settings.AliasSecret),
            Encoding.UTF8.GetBytes(value));

        return "customer_" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}