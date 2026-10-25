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

    [Theory(DisplayName = "TC-00-08 Switching user lands on that role's dashboard: built cards open their pages, unbuilt ones don't link, other dashboards refuse")]
    [Trait("Technique", "Decision table")]
    [InlineData("Customer A", "/Dashboards/Customer", "/Reservations, /Orders", 8)]
    [InlineData("Staff", "/Dashboards/Staff", "/Payments/Counter, /Reservations/Manage, /Orders/Queue", 2)]
    [InlineData("Admin", "/Dashboards/Admin", "", 5)]
    public async Task EachRolesDashboard(string name, string expectedDashboard, string expectedLinks, int expectedNotBuilt)
    {
        var dashboard = await SignInAsAsync(TestUsers.All.Single(user => user.Name == name));
        var cards = dashboard.QuerySelectorAll("main .card").ToList();
        var links = cards.SelectMany(card => card.QuerySelectorAll("a")).Select(a => a.GetAttribute("href")!).ToList();

        Assert.Equal(expectedDashboard, new Uri(dashboard.Url).AbsolutePath);
        Assert.Contains(dashboard.QuerySelectorAll("nav a"), a => TextOf(a) == "Dashboard");
        Assert.Equal(expectedLinks, string.Join(", ", links));
        Assert.Equal(expectedNotBuilt, cards.Count(card => TextOf(card).Contains("Not built yet")));
        foreach (var link in links)
            Assert.Equal(link, new Uri((await GetPageAsync(link)).Url).AbsolutePath);   // opens for this user
        foreach (var other in AllDashboards.Where(path => path != expectedDashboard))
            Assert.Contains($"{name} can't open {other}.", TextOf(await GetPageAsync(other)));
    }
}
