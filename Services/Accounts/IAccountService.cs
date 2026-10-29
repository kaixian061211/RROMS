using RROMS.Web.Models.Accounts;

namespace RROMS.Web.Services.Accounts;

public interface IAccountService
{
    // ---- customers and everyone ----

    /// <summary>Public registration. Always creates a Customer account.</summary>
    Task<AccountResult> RegisterCustomerAsync(RegisterInput input);

    /// <summary>Checks email and password. Fails with the same message for "no such email" and "wrong password".</summary>
    Task<AccountResult> AuthenticateAsync(string email, string password);

    Task<UserAccount?> GetByIdAsync(int userId);

    /// <summary>The role of an active account, or null if it no longer exists or was deactivated.</summary>
    Task<string?> GetActiveRoleAsync(int userId);

    Task<AccountResult> UpdateProfileAsync(int userId, ProfileInput input);

    Task<AccountResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword);

    /// <summary>
    /// Forgot password, step 1: emails a 6-digit code to the account (if it exists and is active).
    /// Returns nothing on purpose, so the page cannot reveal which emails are registered.
    /// </summary>
    Task RequestPasswordResetAsync(string email);

    /// <summary>Forgot password, step 2: checks the code and sets the new password.</summary>
    Task<AccountResult> ResetPasswordWithOtpAsync(string email, string otp, string newPassword);

    // ---- administrator ----

    Task<List<UserAccount>> SearchAsync(string? search, string? role);

    Task<AccountResult> CreateAsync(AdminUserInput input);

    Task<AccountResult> UpdateAsync(int adminUserId, AdminUserInput input);

    Task<AccountResult> SetActiveAsync(int adminUserId, int userId, bool isActive);

    /// <summary>Creates the first administrator if the system has none. Used at start-up.</summary>
    Task EnsureDefaultAdminAsync(string email, string password);
}
