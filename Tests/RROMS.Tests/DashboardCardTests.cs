using RROMS.Web.Models.Dashboards;
using RROMS.Web.Models.Payments;

namespace RROMS.Tests;

/// <summary>Which cards each role's dashboard shows, in and outside Development.</summary>
[Trait("Type", "Unit")]
public class DashboardCardTests
{
    [Theory(DisplayName = "TC-00-07 Dashboard cards: each role sees its own built features, and features not built yet only in Development")]
    [Trait("Technique", "Decision table")]
    [InlineData(PaymentRoles.Customer, true, "1, 5, 7, 8, 12, 13, 15, 19, 20, 21")]
    [InlineData(PaymentRoles.Customer, false, "5, 8, 12, 20")]
    [InlineData(PaymentRoles.Staff, true, "6, 9, 11, 17, 18")]
    [InlineData(PaymentRoles.Staff, false, "6, 9, 11, 17")]
    [InlineData(PaymentRoles.Admin, true, "3, 4, 10, 14, 16")]
    [InlineData(PaymentRoles.Admin, false, "10")]
    public void CardsForEachRole(string role, bool isDevelopment, string expectedStories)
    {
        var cards = DashboardCards.For(role, isDevelopment);

        Assert.Equal(expectedStories, string.Join(", ", cards.Select(card => card.Story)));
        Assert.All(cards, card => Assert.Equal(role, card.Role));
    }
}
