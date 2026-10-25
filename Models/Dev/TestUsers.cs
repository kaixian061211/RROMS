using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Models.Dev;

/// <summary>A pretend user for the Development test login.</summary>
public record TestUser(int Id, string Name, string Role)
{
    /// <summary>The claims the login module will issue: a numeric user id and a role.</summary>
    public ClaimsPrincipal ToPrincipal() => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, Id.ToString(CultureInfo.InvariantCulture)),
        new Claim(ClaimTypes.Name, Name),
        new Claim(ClaimTypes.Role, Role)
    }, CookieAuthenticationDefaults.AuthenticationScheme));
}

/// <summary>
/// The users of the Development test login (/Dev/SwitchUser). Outside Development that page has no address,
/// so nobody can sign in as them.
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
