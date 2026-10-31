using System.Net;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.EntityFrameworkCore;
using RROMS.Tests.Integration;
using RROMS.Web.Models.Dev;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;

namespace RROMS.Tests.Notifications;

[Trait("Type", "Integration")]
public class NotificationWorkflowTests : PageTest
{
    public NotificationWorkflowTests(TestWebApp app) : base(Prepare(app)) { }

    private static TestWebApp Prepare(TestWebApp app)
    {
        TestDatabase.PrepareOnceAsync().GetAwaiter().GetResult();
        return app;
    }

    [Fact]
    public async Task InboxPaginatesNewestFirstWithoutExposingAnotherRecipientsRows()
    {
        await WithDatabaseAsync(async db =>
        {
            for (var i = 0; i < 22; i++) db.Add(Notice(TestCustomer, $"Own notice {i:D2}"));
            db.Add(Notice(OtherCustomer, "Private other notice"));
            await db.SaveChangesAsync();
        });
        var page = await GetPageAsync("/Notifications");
        var articles = page.QuerySelectorAll("main article");
        Assert.Equal(20, articles.Length);
        Assert.Contains("Own notice 21", TextOf(articles[0]));
        Assert.DoesNotContain("Private other notice", TextOf(page));
        Assert.Contains("22 unread", TextOf(page));
        var older = page.QuerySelector<IHtmlAnchorElement>("main a[href*='beforeId=']")!;
        var olderPage = await GetPageAsync(older.Href);
        Assert.Equal(2, olderPage.QuerySelectorAll("main article").Length);
        Assert.Contains("Own notice 00", TextOf(olderPage));
        Assert.DoesNotContain("Private other notice", TextOf(olderPage));
    }

    [Fact]
    public async Task ReadActionsAreIdempotentAndRestrictedToTheSignedInRecipient()
    {
        var ids = await WithDatabaseAsync(async db =>
        {
            var own = Notice(TestCustomer, "Own unread");
            var other = Notice(OtherCustomer, "Other unread");
            db.AddRange(own, other);
            await db.SaveChangesAsync();
            return (Own: own.NotificationId, Other: other.NotificationId);
        });
        var page = await GetPageAsync("/Notifications");
        var form = page.QuerySelectorAll<IHtmlFormElement>("main form")
            .Single(f => f.QuerySelector("input[name=notificationId]") is not null);
        var forbidden = await SendFormAsync(form, new() { ["notificationId"] = ids.Other.ToString() });
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        await SubmitAsync(form);
        await SubmitAsync(form);
        await WithDatabaseAsync(async db =>
        {
            Assert.True((await db.Notifications.FindAsync(ids.Own))!.IsRead);
            Assert.False((await db.Notifications.FindAsync(ids.Other))!.IsRead);
        });
    }

    [Fact]
    public async Task MarkAllRequiresAntiforgeryAndOnlyChangesTheRecipientsRows()
    {
        await WithDatabaseAsync(async db =>
        {
            db.AddRange(Notice(TestCustomer, "Own one"), Notice(TestCustomer, "Own two"), Notice(OtherCustomer, "Other"));
            await db.SaveChangesAsync();
        });
        var invalid = await Client.PostAsync("/Notifications?handler=ReadAll", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var page = await GetPageAsync("/Notifications");
        var form = page.QuerySelectorAll<IHtmlFormElement>("main form").Single(f => f.Action.Contains("handler=ReadAll"));
        var result = await SubmitAsync(form);
        Assert.Contains("0 unread", TextOf(result));
        await WithDatabaseAsync(async db =>
        {
            Assert.Equal(0, await db.Notifications.CountAsync(n => n.UserId == TestCustomer && !n.IsRead));
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == OtherCustomer && !n.IsRead));
        });
    }

    [Fact]
    public async Task TrackingHtmlAndPollingBothEnforceRecordOwnership()
    {
        var records = await WithDatabaseAsync(async db =>
        {
            var own = new Order { CustomerUserId = TestCustomer, TableId = 1 };
            var other = new Order { CustomerUserId = OtherCustomer, TableId = 2 };
            var reservation = new Reservation { CustomerUserId = OtherCustomer, TableId = 2,
                ReservationDate = Tomorrow, TimeSlot = new TimeOnly(12, 0), PartySize = 2 };
            db.AddRange(own, other, reservation);
            await db.SaveChangesAsync();
            var payment = new Payment { OrderId = other.OrderId, Amount = 12, PaymentMethod = PaymentMethod.Card };
            db.Add(payment);
            await db.SaveChangesAsync();
            return (Own: own.OrderId, Other: other.OrderId, Reservation: reservation.ReservationId, Payment: payment.PaymentId);
        });
        var response = await Client.GetAsync($"/Tracking?orderId={records.Own}&handler=Status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(records.Own, json.RootElement.GetProperty("orderId").GetInt32());
        foreach (var selector in new[] { $"orderId={records.Other}", $"reservationId={records.Reservation}", $"paymentId={records.Payment}" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/Tracking?" + selector)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/Tracking?" + selector + "&handler=Status")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/Tracking?orderId=1&paymentId=1&handler=Status")).StatusCode);
        Assert.True((await Client.GetAsync("/Notifications")).Headers.CacheControl!.NoStore);
        await SignInAsAsync(TestUsers.Staff);
        Assert.Contains("AccessDenied", (await Client.GetAsync("/Notifications")).RequestMessage!.RequestUri!.AbsoluteUri);
    }

    private static Notification Notice(int userId, string title) => new()
    {
        UserId = userId, Type = NotificationTypes.Announcement, Title = title, Message = "Fixture notification."
    };
}
