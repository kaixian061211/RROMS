using System.Security.Cryptography;
using RROMS.Web.Services.Accounts;

namespace RROMS.Tests.Accounts;

/// <summary>Stories 1 and 2: passwords are stored as salted PBKDF2 hashes, never as the password.</summary>
[Trait("Type", "Unit")]
[Trait("Story", "1")]
public class PasswordHelperTests
{
    /// <summary>A hash made the way the first version did (100,000 iterations), for the "upgrade at login" tests.</summary>
    public static string LegacyHash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2$100000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    [Fact(DisplayName = "TC-01-01 A password is hashed: the stored text is not the password, and the right password verifies")]
    [Trait("Technique", "Equivalence partitioning")]
    public void HashAndVerify()
    {
        var hash = PasswordHelper.Hash("Secret123");

        Assert.StartsWith("PBKDF2$", hash);
        Assert.DoesNotContain("Secret123", hash);
        Assert.True(PasswordHelper.Verify("Secret123", hash));
        Assert.False(PasswordHelper.Verify("secret123", hash));
        Assert.False(PasswordHelper.Verify("", hash));
    }

    [Fact(DisplayName = "TC-01-02 The same password hashes differently each time (random salt), and both hashes still verify")]
    [Trait("Technique", "Security")]
    public void EachHashHasItsOwnSalt()
    {
        var first = PasswordHelper.Hash("Secret123");
        var second = PasswordHelper.Hash("Secret123");

        Assert.NotEqual(first, second);
        Assert.True(PasswordHelper.Verify("Secret123", first));
        Assert.True(PasswordHelper.Verify("Secret123", second));
    }

    [Fact(DisplayName = "TC-01-03 A hash made with fewer iterations still verifies, and is flagged to be upgraded; a new hash is not")]
    [Trait("Technique", "Equivalence partitioning")]
    public void OldHashesStillWorkAndNeedUpgrading()
    {
        var legacy = LegacyHash("Secret123");

        Assert.True(PasswordHelper.Verify("Secret123", legacy));
        Assert.True(PasswordHelper.NeedsRehash(legacy));
        Assert.False(PasswordHelper.NeedsRehash(PasswordHelper.Hash("Secret123")));
    }

    [Theory(DisplayName = "TC-01-04 A damaged or unknown stored hash never verifies and never crashes the login")]
    [Trait("Technique", "Boundary value analysis")]
    [InlineData("")]
    [InlineData("not a hash")]
    [InlineData("PBKDF2$100000$only-two-parts")]
    [InlineData("PBKDF2$abc$AAAA$AAAA")]
    [InlineData("PBKDF2$100000$!!not-base64!!$!!not-base64!!")]
    [InlineData("MD5$100000$AAAA$AAAA")]
    public void DamagedHashes(string stored)
    {
        Assert.False(PasswordHelper.Verify("Secret123", stored));
    }
}
