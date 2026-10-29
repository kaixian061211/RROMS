using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using RROMS.Web.Models.Accounts;

namespace RROMS.Web.Services.Accounts;

public static class AccountClaims
{
    /// <summary>
    /// Marks a sign-in that belongs to a real row in the UserAccount table. Program.cs re-checks only these on
    /// every request, so the Development test users (/Dev/SwitchUser), who have no row, keep working.
    /// </summary>
    public const string AccountClaimType = "rroms:account";

    /// <summary>
    /// The sign-in cookie content. NameIdentifier is the numeric UserId, which is what the
    /// reservation and payment modules read (User.TryGetCustomerId / TryGetPaymentUserId),
    /// and Role is what [Authorize(Roles = "...")] checks. Email is what the notification module uses to e-mail the customer.
    /// </summary>
    public static ClaimsPrincipal CreatePrincipal(UserAccount user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(AccountClaimType, "1")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public static bool TryGetUserId(this ClaimsPrincipal user, out int userId) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
