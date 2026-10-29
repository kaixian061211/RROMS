using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Services.Accounts;

namespace RROMS.Tests.Accounts;

/// <summary>Stories 1, 2 and 10: register, log in, reset a forgotten password with a one-time code, and admin-created accounts.</summary>
[Trait("Type", "Unit")]
[Trait("Story", "1")]
public class AccountServiceTests : DatabaseTest
{
    /// <summary>Remembers every e-mail instead of sending it, so a test can read the one-time code out of it.</summary>
    private sealed class FakeMailer : IAccountMailer
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            Sent.Add((toEmail, subject, htmlBody));
            return Task.FromResult(true);
        }

        /// <summary>The 6-digit code in the last e-mail sent.</summary>
        public string LastCode() => Regex.Match(Sent[^1].Body, @"\b\d{6}\b").Value;
    }

    private const string Ali = "ali@example.com";

    private AccountService NewService(FakeMailer mailer) => new(NewContext(), mailer);

    private static RegisterInput Registration(string email = Ali, string password = "Secret123") => new()
    {
        FullName = "Ali Hassan", Email = email, Phone = "012-3456789", Password = password, ConfirmPassword = password
    };

    private Task<UserAccount> LoadAsync(string email) =>
        NewContext().Set<UserAccount>().AsNoTracking().SingleAsync(u => u.Email == email);

    /// <summary>A registered customer, ready to forget the password.</summary>
    private async Task<FakeMailer> RegisteredAliAsync()
    {
        var mailer = new FakeMailer();
        Assert.True((await NewService(mailer).RegisterCustomerAsync(Registration())).Succeeded);
        return mailer;
    }

    // ------------------------------------------------------------ register and log in

    [Fact(DisplayName = "TC-01-05 Registering creates an active Customer with a hashed password and a lower-case email")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task RegisterACustomer()
    {
        var result = await NewService(new FakeMailer()).RegisterCustomerAsync(Registration("Ali@Example.COM"));

        var saved = await LoadAsync(Ali);
        Assert.True(result.Succeeded);
        Assert.Equal(UserRoles.Customer, saved.Role);
        Assert.True(saved.IsActive);
        Assert.NotEqual("Secret123", saved.PasswordHash);
        Assert.StartsWith("PBKDF2$", saved.PasswordHash);
    }

    [Fact(DisplayName = "TC-01-06 Registering with an email that is already taken (even in other letter case) is refused")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task DuplicateEmail()
    {
        var mailer = await RegisteredAliAsync();

        var second = await NewService(mailer).RegisterCustomerAsync(Registration("ALI@example.com"));

        Assert.False(second.Succeeded);
        Assert.Equal("This email is already registered.", second.ErrorMessage);
        Assert.Equal(1, await NewContext().Set<UserAccount>().CountAsync(u => u.Email == Ali));
    }

    [Fact(DisplayName = "TC-01-07 Logging in: the right password works; a wrong password and an unknown email get the same message")]
    [Trait("Technique", "Decision table")]
    public async Task LogIn()
    {
        var mailer = await RegisteredAliAsync();

        var right = await NewService(mailer).AuthenticateAsync("ALI@example.com", "Secret123");
        var wrong = await NewService(mailer).AuthenticateAsync(Ali, "Wrong999");
        var unknown = await NewService(mailer).AuthenticateAsync("nobody@example.com", "Secret123");

        Assert.True(right.Succeeded);
        Assert.Equal(Ali, right.User!.Email);
        Assert.False(wrong.Succeeded);
        Assert.False(unknown.Succeeded);
        Assert.Equal(wrong.ErrorMessage, unknown.ErrorMessage);   // does not reveal which emails exist
    }

    [Fact(DisplayName = "TC-02-01 A deactivated account cannot log in, even with the right password")]
    [Trait("Story", "2")]
    [Trait("Technique", "Decision table")]
    public async Task DeactivatedAccount()
    {
        var mailer = new FakeMailer();
        await NewService(mailer).CreateAsync(new AdminUserInput
        {
            FullName = "Siti Staff", Email = "siti@example.com", Role = UserRoles.Staff, IsActive = false, Password = "Staff123"
        });

        var result = await NewService(mailer).AuthenticateAsync("siti@example.com", "Staff123");

        Assert.False(result.Succeeded);
        Assert.Contains("deactivated", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-01-08 A password hashed with fewer iterations is upgraded to the current strength when its owner logs in")]
    [Trait("Technique", "Security")]
    public async Task OldHashIsUpgradedAtLogin()
    {
        var mailer = await RegisteredAliAsync();
        var legacy = PasswordHelperTests.LegacyHash("Secret123");
        await NewContext().Set<UserAccount>().Where(u => u.Email == Ali)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.PasswordHash, legacy));

        var result = await NewService(mailer).AuthenticateAsync(Ali, "Secret123");

        var saved = await LoadAsync(Ali);
        Assert.True(result.Succeeded);
        Assert.False(PasswordHelper.NeedsRehash(saved.PasswordHash));
        Assert.True(PasswordHelper.Verify("Secret123", saved.PasswordHash));
    }

    // ------------------------------------------------------------ forgot password (one-time code)

    [Fact(DisplayName = "TC-01-09 Asking for a reset e-mails a 6-digit code that expires in 10 minutes, and stores only its hash")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task RequestSendsACode()
    {
        var mailer = await RegisteredAliAsync();

        await NewService(mailer).RequestPasswordResetAsync("ALI@example.com");

        var saved = await LoadAsync(Ali);
        var code = mailer.LastCode();
        Assert.Single(mailer.Sent);
        Assert.Equal(Ali, mailer.Sent[0].To);
        Assert.Matches(@"^\d{6}$", code);
        Assert.DoesNotContain(code, saved.ResetOtpHash!);   // the database never holds the code itself
        Assert.InRange(saved.ResetOtpExpiresAt!.Value, DateTime.UtcNow.AddMinutes(9), DateTime.UtcNow.AddMinutes(11));
    }

    [Fact(DisplayName = "TC-01-10 Asking for a reset for an unknown or deactivated account sends nothing and does not fail")]
    [Trait("Technique", "Security")]
    public async Task RequestForNobody()
    {
        var mailer = await RegisteredAliAsync();
        await NewContext().Set<UserAccount>().Where(u => u.Email == Ali)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));

        await NewService(mailer).RequestPasswordResetAsync("nobody@example.com");
        await NewService(mailer).RequestPasswordResetAsync(Ali);

        Assert.Empty(mailer.Sent);
    }

    [Fact(DisplayName = "TC-01-11 The right code sets the new password (the old one stops working), and the code works only once")]
    [Trait("Technique", "Decision table")]
    public async Task ResetWithTheRightCode()
    {
        var mailer = await RegisteredAliAsync();
        await NewService(mailer).RequestPasswordResetAsync(Ali);
        var code = mailer.LastCode();

        var reset = await NewService(mailer).ResetPasswordWithOtpAsync(Ali, code, "Brand-new1");
        var again = await NewService(mailer).ResetPasswordWithOtpAsync(Ali, code, "Another-one2");

        Assert.True(reset.Succeeded);
        Assert.False(again.Succeeded);
        Assert.True((await NewService(mailer).AuthenticateAsync(Ali, "Brand-new1")).Succeeded);
        Assert.False((await NewService(mailer).AuthenticateAsync(Ali, "Secret123")).Succeeded);
        Assert.Null((await LoadAsync(Ali)).ResetOtpHash);
    }

    [Fact(DisplayName = "TC-01-12 A wrong code is refused and the password is unchanged; the right code still works afterwards")]
    [Trait("Technique", "Decision table")]
    public async Task WrongCode()
    {
        var mailer = await RegisteredAliAsync();
        await NewService(mailer).RequestPasswordResetAsync(Ali);
        var code = mailer.LastCode();
        var wrongCode = code == "000000" ? "111111" : "000000";

        var wrong = await NewService(mailer).ResetPasswordWithOtpAsync(Ali, wrongCode, "Brand-new1");
        var attemptsAfterWrong = (await LoadAsync(Ali)).ResetOtpAttempts;
        var passwordStillOld = (await NewService(mailer).AuthenticateAsync(Ali, "Secret123")).Succeeded;
        var right = await NewService(mailer).ResetPasswordWithOtpAsync(Ali, code, "Brand-new1");

        Assert.False(wrong.Succeeded);
        Assert.Equal(1, attemptsAfterWrong);
        Assert.True(passwordStillOld);
        Assert.True(right.Succeeded);
    }

    [Fact(DisplayName = "TC-01-13 After 5 wrong codes the code is thrown away: even the right one is refused")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task TooManyWrongCodes()
    {
        var mailer = await RegisteredAliAsync();
        await NewService(mailer).RequestPasswordResetAsync(Ali);
        var code = mailer.LastCode();
        var wrongCode = code == "000000" ? "111111" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
            Assert.False((await NewService(mailer).ResetPasswordWithOtpAsync(Ali, wrongCode, "Brand-new1")).Succeeded);
        var right = await NewService(mailer).ResetPasswordWithOtpAsync(Ali, code, "Brand-new1");

        Assert.False(right.Succeeded);
        Assert.True((await NewService(mailer).AuthenticateAsync(Ali, "Secret123")).Succeeded);   // password unchanged
    }

    [Fact(DisplayName = "TC-01-14 An expired code is refused")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task ExpiredCode()
    {
        var mailer = await RegisteredAliAsync();
        await NewService(mailer).RequestPasswordResetAsync(Ali);
        var code = mailer.LastCode();
        await NewContext().Set<UserAccount>().Where(u => u.Email == Ali)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.ResetOtpExpiresAt, DateTime.UtcNow.AddMinutes(-1)));

        var result = await NewService(mailer).ResetPasswordWithOtpAsync(Ali, code, "Brand-new1");

        Assert.False(result.Succeeded);
        Assert.True((await NewService(mailer).AuthenticateAsync(Ali, "Secret123")).Succeeded);
    }

    [Fact(DisplayName = "TC-01-15 Asking again within a minute does not send a second code, and the first code still works")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task ResendCooldown()
    {
        var mailer = await RegisteredAliAsync();

        await NewService(mailer).RequestPasswordResetAsync(Ali);
        var code = mailer.LastCode();
        await NewService(mailer).RequestPasswordResetAsync(Ali);

        Assert.Single(mailer.Sent);
        Assert.True((await NewService(mailer).ResetPasswordWithOtpAsync(Ali, code, "Brand-new1")).Succeeded);
    }

    [Fact(DisplayName = "TC-01-16 A new password that is too short is refused without using up the code")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task TooShortNewPassword()
    {
        var mailer = await RegisteredAliAsync();
        await NewService(mailer).RequestPasswordResetAsync(Ali);
        var code = mailer.LastCode();

        var tooShort = await NewService(mailer).ResetPasswordWithOtpAsync(Ali, code, "12345");
        var fine = await NewService(mailer).ResetPasswordWithOtpAsync(Ali, code, "123456");

        Assert.False(tooShort.Succeeded);
        Assert.True(fine.Succeeded);
    }

    // ------------------------------------------------------------ administrator

    [Fact(DisplayName = "TC-10-01 An admin can create a staff account: it has the chosen role and a hashed password")]
    [Trait("Story", "10")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task AdminCreatesStaff()
    {
        var result = await NewService(new FakeMailer()).CreateAsync(new AdminUserInput
        {
            FullName = "Siti Staff", Email = "Siti@Example.com", Role = UserRoles.Staff, IsActive = true, Password = "Staff123"
        });

        var saved = await LoadAsync("siti@example.com");
        Assert.True(result.Succeeded);
        Assert.Equal(UserRoles.Staff, saved.Role);
        Assert.True(PasswordHelper.Verify("Staff123", saved.PasswordHash));
        Assert.NotEqual("Staff123", saved.PasswordHash);
    }
}
