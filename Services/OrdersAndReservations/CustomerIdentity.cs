using System.Security.Claims;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Services.OrdersAndReservations;

public static class CustomerIdentity
{
    // TODO: remove when the login module exists. Development only, so the pages can be tried without login.
    private const int DevelopmentTestCustomerId = 1;

    /// <summary>
    /// The signed-in customer's user id. Until the login module exists, Development falls back to a
    /// test customer; anywhere else there is no customer.
    /// </summary>
    public static bool TryGetCustomerId(this ClaimsPrincipal user, IWebHostEnvironment environment, out int customerUserId)
    {
        if (user.TryGetPaymentUserId(out customerUserId))
            return true;

        // TODO: remove this fallback when the login module exists.
        if (environment.IsDevelopment())
        {
            customerUserId = DevelopmentTestCustomerId;
            return true;
        }

        return false;
    }
}
