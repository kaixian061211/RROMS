using RROMS.Web.Models.Dev;

namespace RROMS.Tests.Integration;

/// <summary>The role dashboards through the pages, in Development (outside it nobody can sign in yet).</summary>
[Trait("Type", "Integration")]
public class DashboardPageTests : PageTest
{
    public DashboardPageTests(TestWebApp app) : base(app) { }

    // Each test chooses who signs in.
    protected override TestUser? SignedInAs => null;

    private static readonly string[] AllDashboards = { "/Dashboards/Customer", "/Dashboards/Staff", "/Dashboards/Admin" };

    [Theory(DisplayName = "TC-00-08 Switching user lands on that role's dashboard: built cards open their pages, placeholders don't link, other dashboards refuse")]
    [Trait("Technique", "Decision table")]
    [InlineData("Customer A", "/Dashboards/Customer", "/Reservations, /Account/Profile, /Orders, /Menu, /Tracking, /Notifications/Index", 3)]
    [InlineData("Staff", "/Dashboards/Staff", "/Payments/Counter, /Account/Profile, /Reservations/Manage, /Orders/Queue", 1)]
    [InlineData("Admin", "/Dashboards/Admin",
        "/Restaurant/Manage, /Restaurant/Manage/Announcements, /Menu/Manage, /Admin/Accounts/Index, /Promotions/Manage, /Reports, " +
        "/Payments/Counter, /Reservations/Manage, /Orders/Queue", 0)]
    public async Task EachRolesDashboard(string name, string expectedDashboard, string expectedLinks, int expectedPlaceholders)
    {
        var dashboard = await SignInAsAsync(TestUsers.All.Single(user => user.Name == name));
        var cards = dashboard.QuerySelectorAll("main .rr-feature").ToList();
        var links = cards.SelectMany(card => card.QuerySelectorAll("a")).Select(a => a.GetAttribute("href")!).ToList();

        Assert.Equal(expectedDashboard, new Uri(dashboard.Url).AbsolutePath);
        Assert.Contains(dashboard.QuerySelectorAll("nav a"), a => TextOf(a) == "Dashboard");
        Assert.Equal(expectedLinks, string.Join(", ", links));
        Assert.Equal(expectedPlaceholders, cards.Count(card => card.LocalName == "button" && TextOf(card).Contains("Coming soon")));
        foreach (var link in links)
            Assert.Equal(link, new Uri((await GetPageAsync(link)).Url).AbsolutePath);   // opens for this user
        foreach (var other in AllDashboards.Where(path => path != expectedDashboard))
            Assert.Equal("/Account/AccessDenied", new Uri((await GetPageAsync(other)).Url).AbsolutePath);
    }
}
