using System.Security.Cryptography;

namespace RROMS.Web.Services.Accounts;

/// <summary>
/// Password hashing with PBKDF2-HMAC-SHA256 (built into .NET, no extra package needed).
/// Stored format: PBKDF2$iterations$saltBase64$hashBase64 (about 85 characters).
/// Every password gets its own random salt, and the iteration count is stored inside the hash string,
/// so the count can be raised later without breaking existing accounts (see NeedsRehash).
/// </summary>
public static class PasswordHelper
{
    private const string Prefix = "PBKDF2";
    private const int Iterations = 600_000;   // OWASP recommendation for PBKDF2-HMAC-SHA256
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out int iterations) || iterations <= 0)
            return false;

        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

            // constant-time comparison, so the time taken does not leak how much of the hash matched
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;   // a damaged value in the database must not crash the login page
        }
    }

    /// <summary>True when a stored hash was made with fewer iterations than we use now.</summary>
    public static bool NeedsRehash(string stored)
    {
        var parts = stored.Split('$');
        return parts.Length == 4
            && parts[0] == Prefix
            && int.TryParse(parts[1], out int iterations)
            && iterations < Iterations;
    }
}
