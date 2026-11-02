using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Services.Notifications;

namespace RROMS.Tests.Notifications;

[Trait("Type", "Unit")]
public class NotificationPushTests
{
    [Fact]
    public async Task PushTargetsAnOpaqueOwnerAliasAndReusesItsIdempotencyKey()
    {
        var settings = Options.Create(new WebPushSettings
        {
            AppId = Guid.NewGuid().ToString(), ApiKey = "test-key", AliasSecret = new string('x', 32),
            PublicBaseUrl = "https://restaurant.test/rroms/"
        });
        var requests = new RecordingHttp();
        using var http = new HttpClient(requests);
        var identity = new WebPushIdentity(settings);
        var sender = new OneSignalPushSender(http, settings, identity, NullLogger<OneSignalPushSender>.Instance);
        var notice = new Notification { NotificationId = 42, UserId = 9001, OrderId = 7,
            Title = "Order ready", Message = "Collect your order.", CreatedAt = new DateTime(2026, 10, 31, 12, 0, 0) };
        await sender.SendAsync(notice);
        await sender.SendAsync(notice);
        var first = requests.Payloads[0];
        Assert.Equal("https://restaurant.test/rroms/Tracking?orderId=7", first.GetProperty("url").GetString());
        var alias = first.GetProperty("include_aliases").GetProperty("external_id")[0].GetString();
        Assert.Equal(identity.GetExternalId(9001), alias);
        Assert.NotEqual(identity.GetExternalId(9002), alias);
        Assert.Equal(first.GetProperty("idempotency_key").GetString(), requests.Payloads[1].GetProperty("idempotency_key").GetString());
        Assert.True(Guid.TryParse(first.GetProperty("idempotency_key").GetString(), out _));
    }

    [Fact]
    public async Task AnUnacceptedPushIsReportedWithoutCallingARealProvider()
    {
        var settings = Options.Create(new WebPushSettings { AppId = Guid.NewGuid().ToString(), ApiKey = "test",
            AliasSecret = new string('x', 32), PublicBaseUrl = "https://restaurant.test" });
        using var http = new HttpClient(new RecordingHttp { Response = "{}" });
        var sender = new OneSignalPushSender(http, settings, new WebPushIdentity(settings), NullLogger<OneSignalPushSender>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(new Notification
        { NotificationId = 1, UserId = 9001, Title = "Fixture", Message = "No delivery." }));
    }

    private sealed class RecordingHttp : HttpMessageHandler
    {
        public List<JsonElement> Payloads { get; } = new();
        public string Response { get; init; } = "{\"id\":\"accepted-fixture\"}";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Payloads.Add(json.RootElement.Clone());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Response) };
        }
    }
}
