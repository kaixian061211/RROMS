using RROMS.Web.Models.Payments;

namespace RROMS.Tests.Integration;

/// <summary>A fixture account the page tests sign in as, through Switch user (/Dev/SwitchUser).</summary>
public record TestUser(int Id, string Name, string Role);

/// <summary>
/// The fixture accounts the page tests sign in as. TestDatabase gives each one a UserAccount row with this id and
/// role ("Fixture Customer A" and so on), so they sign in exactly as the real login would sign them in.
/// </summary>
public static class TestUsers
{
    // Customer A has the id my pages used for their test customer before, so existing test data stays theirs.
    public static readonly TestUser CustomerA = new(1, "Customer A", PaymentRoles.Customer);
    public static readonly TestUser CustomerB = new(2, "Customer B", PaymentRoles.Customer);
    public static readonly TestUser Staff = new(3, "Staff", PaymentRoles.Staff);
    public static readonly TestUser Admin = new(4, "Admin", PaymentRoles.Admin);

    public static readonly IReadOnlyList<TestUser> All = new[] { CustomerA, CustomerB, Staff, Admin };

    public static TestUser? Find(int id) => All.FirstOrDefault(user => user.Id == id);
}
