using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.Accounts;
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

        if (path == "/Account/Login")
            return "sent to sign in";
        if (path == "/Account/AccessDenied")
            return "refused";
        return $"unexpected redirect: {path}";
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

        Assert.Equal("/Account/AccessDenied", response.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Equal(OrderStatus.Pending,
            await WithDatabaseAsync(db => db.Set<Order>().Where(o => o.OrderId == order.OrderId).Select(o => o.Status).SingleAsync()));
    }

    [Theory(DisplayName = "TC-00-06 Switch user signs each fixture account in as itself (name, role, user id) and lands on its dashboard; Sign out signs them out")]
    [Trait("Technique", "Equivalence partitioning")]
    [InlineData("Customer A", 1, PaymentRoles.Customer, "/Dashboards/Customer")]
    [InlineData("Customer B", 2, PaymentRoles.Customer, "/Dashboards/Customer")]
    [InlineData("Staff", 3, PaymentRoles.Staff, "/Dashboards/Staff")]
    [InlineData("Admin", 4, PaymentRoles.Admin, "/Dashboards/Admin")]
    public async Task SwitchUser(string name, int expectedId, string expectedRole, string expectedDashboard)
    {
        var dashboard = await SignInAsAsync(UserNamed(name));
        var signedIn = TestUserBar(await GetPageAsync("/"));
        await SignOutAsync();
        var signedOut = TestUserBar(await GetPageAsync("/"));

        Assert.Equal(expectedDashboard, new Uri(dashboard.Url).AbsolutePath);
        // The fixture account's row (TestDatabase) is "Fixture Customer A" and so on.
        Assert.Contains($"Signed in as Fixture {name} · {expectedRole} · user #{expectedId}", signedIn);
        Assert.Contains("Not signed in", signedOut);
    }

    private static IHtmlFormElement? SwitchUserButton(IHtmlDocument switchPage, int accountId) =>
        switchPage.Forms.SingleOrDefault(f => f.QuerySelector<IHtmlInputElement>("input[name=id]")?.Value == accountId.ToString());

    [Fact(DisplayName = "TC-00-09 Switch user signs in like the real login: deactivating the account signs it out, and a deactivated account is neither listed nor accepted")]
    [Trait("Technique", "State transition")]
    public async Task DeactivatedAccount()
    {
        const int bob = 9002;   // Fixture Bob, an active customer (TestDatabase)
        var switchPage = await GetPageAsync("/Dev/SwitchUser");
        await SubmitAsync(SwitchUserButton(switchPage, bob)!);
        var signedIn = TestUserBar(await GetPageAsync("/"));

        await SetActiveAsync(bob, false);
        try
        {
            // The sign-in carries the account marker, so the cookie check (Program.cs) sees the deactivation.
            var afterDeactivation = TestUserBar(await GetPageAsync("/"));
            var listed = SwitchUserButton(await GetPageAsync("/Dev/SwitchUser"), bob);
            var staleButton = await SendFormAsync(SwitchUserButton(switchPage, bob)!);   // from the page loaded earlier

            Assert.Contains($"Signed in as Fixture Bob · {PaymentRoles.Customer} · user #{bob}", signedIn);
            Assert.Contains("Not signed in", afterDeactivation);
            Assert.Null(listed);
            Assert.Equal(HttpStatusCode.NotFound, staleButton.StatusCode);
        }
        finally
        {
            // The fixture accounts are kept between tests, so put this one back.
            await SetActiveAsync(bob, true);
        }
    }

    private Task SetActiveAsync(int userId, bool isActive) =>
        WithDatabaseAsync(db => db.Set<UserAccount>().Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, isActive)));

    private static string TestUserBar(IHtmlDocument page) => TextOf(page.QuerySelector(".test-user-bar")!);
}
