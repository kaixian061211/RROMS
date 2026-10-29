namespace RROMS.Web.Models.Accounts;

/// <summary>
/// Role names stored in UserAccount.Role and put in the sign-in cookie.
/// They match PaymentRoles ("Admin", "Staff", "Customer"), so
/// [Authorize(Roles = PaymentRoles.AdminOrStaff)] works with this login module.
/// They must also match the CHECK constraint on the UserAccount table.
/// </summary>
public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Customer = "Customer";

    public const string AdminOrStaff = Admin + "," + Staff;

    public static readonly string[] All = { Admin, Staff, Customer };
    public static readonly string[] StaffRoles = { Admin, Staff };
}
