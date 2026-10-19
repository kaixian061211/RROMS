using System.Security.Claims;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Services.OrdersAndReservations;

public static class StaffIdentity
{
    /// <summary>
    /// True for Staff and Admin users. Until the login module exists, Development lets everyone in
    /// so the staff pages can be tried; anywhere else nobody gets in.
    /// </summary>
    public static bool IsStaff(this ClaimsPrincipal user, IWebHostEnvironment environment)
    {
        if (user.IsInRole(PaymentRoles.Staff) || user.IsInRole(PaymentRoles.Admin))
            return true;

        // TODO: remove this fallback when the login module exists, and uncomment [Authorize] on the staff pages.
        return environment.IsDevelopment();
    }
}
