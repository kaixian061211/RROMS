using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.Accounts;

/// <summary>
/// One table for every account (customer, staff and admin). UserId is the id other modules store as
/// CustomerUserId / RecordedByStaffId and read from the NameIdentifier claim after login.
/// </summary>
public class UserAccount
{
    public int UserId { get; set; }

    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }

    [Required, StringLength(20)]
    public string Role { get; set; } = UserRoles.Customer;

    /// <summary>PBKDF2 hash, never the real password.</summary>
    [Required, StringLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>False = deactivated: cannot log in, but reservations, orders and payments are kept.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    // ---- "Forgot password" one-time code (OTP) ----

    /// <summary>PBKDF2 hash of the pending reset code, never the code itself. Null = no reset pending.</summary>
    [StringLength(200)]
    public string? ResetOtpHash { get; set; }

    /// <summary>When the pending code stops working (UTC).</summary>
    public DateTime? ResetOtpExpiresAt { get; set; }

    /// <summary>Wrong guesses for the pending code. The code is thrown away after too many.</summary>
    public int ResetOtpAttempts { get; set; }
}
