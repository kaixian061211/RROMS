namespace RROMS.Web.Models.Accounts;

/// <summary>
/// What an account action returns. Expected failures, like a duplicate email or a wrong password,
/// carry a message for the page to show instead of throwing an exception.
/// </summary>
public class AccountResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public UserAccount? User { get; init; }

    public static AccountResult Success(UserAccount? user = null) =>
        new() { Succeeded = true, User = user };

    public static AccountResult Failure(string errorMessage) =>
        new() { ErrorMessage = errorMessage };
}
