namespace RROMS.Web.Models.Payments;

/// <summary>
/// Role names the payment pages are authorised with. Change them here if your login
/// module names its roles differently.
/// </summary>
public static class PaymentRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Customer = "Customer";

    public const string AdminOrStaff = Admin + "," + Staff;
}