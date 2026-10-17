using System.Security.Claims;

namespace RROMS.Web.Services.Payments;

public static class PaymentUserIdentity
{
    public static int GetPaymentUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException(
                "The signed-in user has no numeric user id in the NameIdentifier claim (see PAYMENT_SETUP.md).");
    }
}