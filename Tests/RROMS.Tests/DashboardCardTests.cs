using RROMS.Web.Models.Dashboards;
using RROMS.Web.Models.Payments;

namespace RROMS.Tests;

/// <summary>Which cards each role's dashboard shows, and which of them are "Coming soon" placeholders.</summary>
[Trait("Type", "Unit")]
public class DashboardCardTests
{
    [Theory(DisplayName = "TC-00-07 Dashboard cards: each role sees its own features, and features not built yet as placeholders")]
    [Trait("Technique", "Decision table")]
    [InlineData(PaymentRoles.Customer, "5, 7, 8, 12, 13, 15, 19, 20, 21", "")]
    [InlineData(PaymentRoles.Staff, "6, 9, 11, 17, 18", "")]
    [InlineData(PaymentRoles.Admin, "3, 3, 4, 10, 14, 16", "")]
    public void CardsForEachRole(string role, string expectedStories, string expectedPlaceholders)
    {
        var cards = DashboardCards.For(role);

        Assert.Equal(expectedStories, string.Join(", ", cards.Select(card => card.Story)));
        Assert.Equal(expectedPlaceholders, string.Join(", ", cards.Where(card => !card.IsBuilt).Select(card => card.Story)));
        Assert.All(cards, card => Assert.Equal(role, card.Role));
    }
}
