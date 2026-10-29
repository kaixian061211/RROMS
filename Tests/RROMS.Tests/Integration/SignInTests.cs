using AngleSharp.Html.Dom;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.Dev;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Tests.Integration;

/// <summary>
/// The Development test login, and who it lets into my pages. The real login must issue the same claims, so
/// these rules stay the same once it exists.
/// </summary>
[Trait("Type", "Integration")]
public class SignInTests : PageTest
{
    public SignInTests(TestWebApp app) : base(app) { }

    // Each test chooses who signs in.
    protected override TestUser? SignedInAs => null;

    private static TestUser UserNamed(string name) => TestUsers.All.Single(user => user.Name == name);

    /// <summary>Opens a page, following any redirect, and says what happened: "opens", "sent to sign in" or "refused".</summary>
    private async Task<string> TryToOpenAsync(string url)
    {
        var page = await GetPageAsync(url);
        var path = new Uri(page.Url).AbsolutePath;
        if (path == url)
            return "opens";

        Assert.Equal("/Dev/SwitchUser", path);
        var text = TextOf(page);
        if (text.Contains($"Choose a test user to open {url}."))
            return "sent to sign in";
        if (text.Contains($"can't open {url}. Choose a user who can."))
            return "refused";
        return $"unexpected: {text}";
    }

    [Theory(DisplayName = "TC-05-30 Who can open the customer pages: customers get in, a visitor is sent to sign in, staff and admin are refused")]
    [Trait("Story", "5")]
    [Trait("Story", "12")]
    [Trait("Technique", "Decision table")]
    [InlineData(null, "sent to sign in")]
    [InlineData("Customer A", "opens")]
    [InlineData("Customer B", "opens")]
    [InlineData("Staff", "refused")]
    [InlineData("Admin", "refused")]
    public async Task WhoCanOpenTheCustomerPages(string? user, string expected)
    {
        if (user is not null)
            await SignInAsAsync(UserNamed(user));

        foreach (var url in new[] { "/Reservations", "/Reservations/Create", "/Orders", "/Orders/Create" })
            Assert.Equal((url, expected), (url, await TryToOpenAsync(url)));
    }

    [Theory(DisplayName = "TC-11-13 Who can open the staff pages: staff and admin get in, a visitor is sent to sign in, a customer is refused")]
    [Trait("Story", "11")]
    [Trait("Story", "17")]
    [Trait("Technique", "Decision table")]
    [InlineData(null, "sent to sign in")]
    [InlineData("Customer A", "refused")]
    [InlineData("Staff", "opens")]
    [InlineData("Admin", "opens")]
    public async Task WhoCanOpenTheStaffPages(string? user, string expected)
    {
        if (user is not null)
            await SignInAsAsync(UserNamed(user));

        foreach (var url in new[] { "/Reservations/Manage", "/Reservations/Manage/Upcoming", "/Orders/Queue" })
            Assert.Equal((url, expected), (url, await TryToOpenAsync(url)));
    }

    [Fact(DisplayName = "TC-17-28 A customer sending a staff status button (copied from the order queue) is refused, and the order is unchanged")]
    [Trait("Story", "17")]
    [Trait("Technique", "Security")]
    public async Task CustomerSendsAStaffButton()
    {
        var order = new Order
        {
            CustomerUserId = TestCustomer, TableId = 4, Status = OrderStatus.Pending,
            Items = { new OrderItem { MenuItemId = TehTarik, Quantity = 1, UnitPrice = 3.80m } }
        };
        await WithDatabaseAsync(db => { db.Add(order); return db.SaveChangesAsync(); });
        await SignInAsAsync(TestUsers.Staff);
        var startPreparing = StatusButton(await GetPageAsync("/Orders/Queue"), order.OrderId, "Start preparing")!;

        await SignInAsAsync(TestUsers.CustomerA);
        var response = await SendFormAsync(startPreparing);

        Assert.Equal("/Dev/SwitchUser", response.RequestMessage!.RequestUri!.AbsolutePath);   // the "can't open" page
        Assert.Equal(OrderStatus.Pending,
            await WithDatabaseAsync(db => db.Set<Order>().Where(o => o.OrderId == order.OrderId).Select(o => o.Status).SingleAsync()));
    }

    [Theory(DisplayName = "TC-00-06 The test login signs each user in with the claims the real login will use, and Sign out signs them out")]
    [Trait("Technique", "Equivalence partitioning")]
    [InlineData("Customer A", 1, PaymentRoles.Customer)]
    [InlineData("Customer B", 2, PaymentRoles.Customer)]
    [InlineData("Staff", 3, PaymentRoles.Staff)]
    [InlineData("Admin", 4, PaymentRoles.Admin)]
    public async Task SwitchUser(string name, int expectedId, string expectedRole)
    {
        var user = UserNamed(name);

        await SignInAsAsync(user);
        var signedIn = TestUserBar(await GetPageAsync("/"));
        await SignOutAsync();
        var signedOut = TestUserBar(await GetPageAsync("/"));

        Assert.Equal(expectedId, user.ToPrincipal().GetPaymentUserId());
        Assert.True(user.ToPrincipal().IsInRole(expectedRole));
        Assert.Contains($"Signed in as {name} · {expectedRole} · user #{expectedId}", signedIn);
        Assert.Contains("Not signed in", signedOut);
    }

    private static string TestUserBar(IHtmlDocument page) => TextOf(page.QuerySelector(".test-user-bar")!);
}
