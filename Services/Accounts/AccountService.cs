using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Accounts;

namespace RROMS.Web.Services.Accounts;

public class AccountService : IAccountService
{
    private const string InvalidLoginMessage = "Invalid email or password.";
    private const string DeactivatedMessage = "This account has been deactivated. Please contact the restaurant.";
    private const string EmailTakenMessage = "This email is already registered.";
    private const string NotFoundMessage = "Account not found.";
    private const string WrongPasswordMessage = "Current password is incorrect.";
    private const string InvalidRoleMessage = "Please choose a valid role.";
    private const string PasswordRequiredMessage = "Password is required (at least 6 characters).";
    private const string LastAdminMessage =
        "You cannot remove the Admin role from, or deactivate, the last active administrator " +
        "(this also covers your own account).";

    private const string InvalidOtpMessage = "The code is invalid or has expired. Please request a new one.";

    private const int MinPasswordLength = 6;
    private const int OtpLifetimeMinutes = 10;   // how long a code works
    private const int OtpResendSeconds = 60;     // minimum gap between two codes for the same account
    private const int OtpMaxAttempts = 5;        // wrong guesses before the code is thrown away

    private readonly AppDbContext _db;
    private readonly IAccountMailer _mailer;

    public AccountService(AppDbContext db, IAccountMailer mailer)
    {
        _db = db;
        _mailer = mailer;
    }

    // AppDbContext picks up UserAccount through UserAccountConfiguration, so it needs no DbSet for it.
    private DbSet<UserAccount> Users => _db.Set<UserAccount>();

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string? CleanPhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

    // ---------------------------------------------------------------- everyone

    public async Task<AccountResult> RegisterCustomerAsync(RegisterInput input)
    {
        var email = NormalizeEmail(input.Email);
        if (await Users.AnyAsync(u => u.Email == email))
            return AccountResult.Failure(EmailTakenMessage);

        var user = new UserAccount
        {
            FullName = input.FullName.Trim(),
            Email = email,
            Phone = CleanPhone(input.Phone),
            Role = UserRoles.Customer,           // public registration can never create staff or admin
            PasswordHash = PasswordHelper.Hash(input.Password),
            IsActive = true
        };
        Users.Add(user);
        return await SaveAsync(user);
    }

    public async Task<AccountResult> AuthenticateAsync(string email, string password)
    {
        var normalized = NormalizeEmail(email);
        var user = await Users.FirstOrDefaultAsync(u => u.Email == normalized);

        // Same message whether the email or the password is wrong, so we do not reveal which emails exist.
        if (user is null || !PasswordHelper.Verify(password, user.PasswordHash))
            return AccountResult.Failure(InvalidLoginMessage);

        if (!user.IsActive)
            return AccountResult.Failure(DeactivatedMessage);

        // Passwords hashed with an older (weaker) iteration count are upgraded silently at login,
        // the only moment we know the real password.
        if (PasswordHelper.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = PasswordHelper.Hash(password);
            await _db.SaveChangesAsync();
        }

        return AccountResult.Success(user);
    }

    public Task<UserAccount?> GetByIdAsync(int userId) =>
        Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);

    public Task<string?> GetActiveRoleAsync(int userId) =>
        Users.AsNoTracking()
             .Where(u => u.UserId == userId && u.IsActive)
             .Select(u => (string?)u.Role)
             .FirstOrDefaultAsync();

    public async Task<AccountResult> UpdateProfileAsync(int userId, ProfileInput input)
    {
        var user = await Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null) return AccountResult.Failure(NotFoundMessage);

        var email = NormalizeEmail(input.Email);
        if (await Users.AnyAsync(u => u.Email == email && u.UserId != userId))
            return AccountResult.Failure(EmailTakenMessage);

        user.FullName = input.FullName.Trim();
        user.Email = email;
        user.Phone = CleanPhone(input.Phone);
        return await SaveAsync(user);
    }

    public async Task<AccountResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null) return AccountResult.Failure(NotFoundMessage);

        if (!PasswordHelper.Verify(currentPassword, user.PasswordHash))
            return AccountResult.Failure(WrongPasswordMessage);

        if (newPassword.Length < MinPasswordLength)
            return AccountResult.Failure(PasswordRequiredMessage);

        user.PasswordHash = PasswordHelper.Hash(newPassword);
        return await SaveAsync(user);
    }

    // ---------------------------------------------------------------- forgot password (OTP)

    public async Task RequestPasswordResetAsync(string email)
    {
        var normalized = NormalizeEmail(email);
        var user = await Users.FirstOrDefaultAsync(u => u.Email == normalized);

        // Unknown or deactivated email: do nothing (the page shows the same message either way).
        if (user is null || !user.IsActive) return;

        var now = DateTime.UtcNow;

        // Resend cooldown: a code sent less than OtpResendSeconds ago is still the current one.
        if (user.ResetOtpHash is not null && user.ResetOtpExpiresAt is { } expires
            && expires.AddMinutes(-OtpLifetimeMinutes) > now.AddSeconds(-OtpResendSeconds))
            return;

        // Cryptographically secure 6 digits, e.g. "047291"
        string otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        // Only the hash is stored, so a leaked database does not leak usable codes.
        user.ResetOtpHash = PasswordHelper.Hash(otp);
        user.ResetOtpExpiresAt = now.AddMinutes(OtpLifetimeMinutes);
        user.ResetOtpAttempts = 0;
        await _db.SaveChangesAsync();

        var (subject, body) = AccountEmails.PasswordResetOtp(user.FullName, otp, OtpLifetimeMinutes);
        await _mailer.SendAsync(user.Email, user.FullName, subject, body);
    }

    public async Task<AccountResult> ResetPasswordWithOtpAsync(string email, string otp, string newPassword)
    {
        if (newPassword.Length < MinPasswordLength)
            return AccountResult.Failure(PasswordRequiredMessage);

        var normalized = NormalizeEmail(email);
        var user = await Users.FirstOrDefaultAsync(u => u.Email == normalized && u.IsActive);

        // One message for "no such account", "no code requested", "expired" and "wrong code".
        if (user?.ResetOtpHash is null || user.ResetOtpExpiresAt is null || user.ResetOtpExpiresAt < DateTime.UtcNow)
            return AccountResult.Failure(InvalidOtpMessage);

        if (!PasswordHelper.Verify(otp.Trim(), user.ResetOtpHash))
        {
            user.ResetOtpAttempts++;
            if (user.ResetOtpAttempts >= OtpMaxAttempts)
                ClearResetOtp(user);   // too many guesses: the code is dead, they must request a new one
            await _db.SaveChangesAsync();
            return AccountResult.Failure(InvalidOtpMessage);
        }

        user.PasswordHash = PasswordHelper.Hash(newPassword);
        ClearResetOtp(user);           // a code works only once
        return await SaveAsync(user);
    }

    private static void ClearResetOtp(UserAccount user)
    {
        user.ResetOtpHash = null;
        user.ResetOtpExpiresAt = null;
        user.ResetOtpAttempts = 0;
    }

    // ---------------------------------------------------------------- administrator

    public async Task<List<UserAccount>> SearchAsync(string? search, string? role)
    {
        var query = Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.FullName.Contains(term) || u.Email.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(role) && UserRoles.All.Contains(role))
            query = query.Where(u => u.Role == role);

        return await query.OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync();
    }

    public async Task<AccountResult> CreateAsync(AdminUserInput input)
    {
        if (!UserRoles.All.Contains(input.Role))
            return AccountResult.Failure(InvalidRoleMessage);

        if (string.IsNullOrEmpty(input.Password) || input.Password.Length < MinPasswordLength)
            return AccountResult.Failure(PasswordRequiredMessage);

        var email = NormalizeEmail(input.Email);
        if (await Users.AnyAsync(u => u.Email == email))
            return AccountResult.Failure(EmailTakenMessage);

        var user = new UserAccount
        {
            FullName = input.FullName.Trim(),
            Email = email,
            Phone = CleanPhone(input.Phone),
            Role = input.Role,
            PasswordHash = PasswordHelper.Hash(input.Password),
            IsActive = input.IsActive
        };
        Users.Add(user);
        return await SaveAsync(user);
    }

    public async Task<AccountResult> UpdateAsync(int adminUserId, AdminUserInput input)
    {
        if (!UserRoles.All.Contains(input.Role))
            return AccountResult.Failure(InvalidRoleMessage);

        var user = await Users.FirstOrDefaultAsync(u => u.UserId == input.UserId);
        if (user is null) return AccountResult.Failure(NotFoundMessage);

        var email = NormalizeEmail(input.Email);
        if (await Users.AnyAsync(u => u.Email == email && u.UserId != user.UserId))
            return AccountResult.Failure(EmailTakenMessage);

        if (!string.IsNullOrEmpty(input.Password) && input.Password.Length < MinPasswordLength)
            return AccountResult.Failure(PasswordRequiredMessage);

        if (await WouldRemoveLastAdminAsync(user, input.Role, input.IsActive))
            return AccountResult.Failure(LastAdminMessage);

        // An administrator cannot demote or deactivate themselves (prevents locking yourself out).
        if (user.UserId == adminUserId && (input.Role != UserRoles.Admin || !input.IsActive))
            return AccountResult.Failure("You cannot remove your own Admin role or deactivate your own account.");

        user.FullName = input.FullName.Trim();
        user.Email = email;
        user.Phone = CleanPhone(input.Phone);
        user.Role = input.Role;
        user.IsActive = input.IsActive;
        if (!string.IsNullOrEmpty(input.Password))
            user.PasswordHash = PasswordHelper.Hash(input.Password);

        return await SaveAsync(user);
    }

    public async Task<AccountResult> SetActiveAsync(int adminUserId, int userId, bool isActive)
    {
        var user = await Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null) return AccountResult.Failure(NotFoundMessage);

        if (user.UserId == adminUserId && !isActive)
            return AccountResult.Failure("You cannot deactivate your own account.");

        if (await WouldRemoveLastAdminAsync(user, user.Role, isActive))
            return AccountResult.Failure(LastAdminMessage);

        user.IsActive = isActive;
        return await SaveAsync(user);
    }

    public async Task EnsureDefaultAdminAsync(string email, string password)
    {
        if (await Users.AnyAsync(u => u.Role == UserRoles.Admin))
            return;

        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        Users.Add(new UserAccount
        {
            FullName = "System Administrator",
            Email = NormalizeEmail(email),
            Role = UserRoles.Admin,
            PasswordHash = PasswordHelper.Hash(password),
            IsActive = true
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another instance of the app created the administrator at the same moment (the unique email index stopped ours).
        }
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>True if this change would leave the system with no active administrator.</summary>
    private async Task<bool> WouldRemoveLastAdminAsync(UserAccount user, string newRole, bool newActive)
    {
        bool isActiveAdminNow = user.Role == UserRoles.Admin && user.IsActive;
        bool stillActiveAdmin = newRole == UserRoles.Admin && newActive;
        if (!isActiveAdminNow || stillActiveAdmin) return false;

        bool otherActiveAdmin = await Users.AnyAsync(u =>
            u.UserId != user.UserId && u.Role == UserRoles.Admin && u.IsActive);
        return !otherActiveAdmin;
    }

    private async Task<AccountResult> SaveAsync(UserAccount user)
    {
        try
        {
            await _db.SaveChangesAsync();
            return AccountResult.Success(user);
        }
        // 2601 / 2627 = unique index violation: someone registered the same email at the same moment
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return AccountResult.Failure(EmailTakenMessage);
        }
    }
}
