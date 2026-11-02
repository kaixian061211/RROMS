using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RROMS.Tests.Integration;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Services.Notifications;

namespace RROMS.Tests.Notifications;

[Trait("Type", "Integration")]
public class NotificationRealtimeTransportTests : PageTest
{
    private readonly TestWebApp _app;

    public NotificationRealtimeTransportTests(TestWebApp app) : base(Prepare(app)) => _app = app;

    private static TestWebApp Prepare(TestWebApp app)
    {
        TestDatabase.PrepareOnceAsync().GetAwaiter().GetResult();
        return app;
    }

    [Fact]
    public async Task SignalRDeliversOnlyTheOwnersEventsAndAnswersUnreadCount()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var token = deadline.Token;
        using var negotiation = await Client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null, token);
        negotiation.EnsureSuccessStatusCode();
        using var negotiated = JsonDocument.Parse(await negotiation.Content.ReadAsStringAsync(token));
        var endpoint = "/hubs/notifications?id=" + Uri.EscapeDataString(
            negotiated.RootElement.GetProperty("connectionToken").GetString()!);
        try
        {
            // Exercise the real SignalR long-polling transport and JSON protocol.
            using var initialPoll = await Client.GetAsync(endpoint, token);
            initialPoll.EnsureSuccessStatusCode();
            await SendAsync("{\"protocol\":\"json\",\"version\":1}");
            Assert.Contains("{}\u001e", await PollAsync());

            var own = new Notification
            {
                UserId = TestCustomer, Type = NotificationTypes.Announcement,
                Title = "Owner event", Message = "Only this customer receives this."
            };
            await WithDatabaseAsync(async db => { db.Add(own); await db.SaveChangesAsync(token); });
            using var scope = _app.Services.CreateScope();
            var realtime = scope.ServiceProvider.GetRequiredService<INotificationRealtime>();
            await realtime.PublishAsync(new Notification
            {
                NotificationId = own.NotificationId + 1, UserId = OtherCustomer,
                Title = "Other customer event", Message = "Private other message"
            }, token);
            await realtime.PublishAsync(own, token);
            var notification = await PollAsync();
            Assert.Contains("NotificationReceived", notification);
            Assert.Contains("Owner event", notification);
            Assert.DoesNotContain("Other customer event", notification);

            await SendAsync("{\"type\":1,\"invocationId\":\"count\",\"target\":\"GetUnreadCount\",\"arguments\":[]}");
            var count = await PollAsync();
            Assert.Contains("\"invocationId\":\"count\"", count);
            Assert.Contains("\"result\":1", count);

            var service = scope.ServiceProvider.GetRequiredService<INotificationService>();
            Assert.True(await service.MarkAsReadAsync(TestCustomer, own.NotificationId, token));
            Assert.Contains("NotificationReadStateChanged", await PollAsync());
        }
        finally
        {
            using var closed = await Client.DeleteAsync(endpoint, token);
        }

        async Task SendAsync(string json)
        {
            using var content = new StringContent(json + '\u001e', Encoding.UTF8, "text/plain");
            using var response = await Client.PostAsync(endpoint, content, token);
            response.EnsureSuccessStatusCode();
        }

        async Task<string> PollAsync()
        {
            using var response = await Client.GetAsync(endpoint, token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(token);
        }
    }
}
