namespace RROMS.Web.Models.CustomerSupportAndFeedback;

/// <summary>
/// What a feedback or request action returns. Expected failures, like rating an order that isn't
/// completed, carry a message for the page to show instead of throwing an exception.
/// </summary>
public class SupportResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }

    public static SupportResult Success() => new() { Succeeded = true };

    public static SupportResult Failure(string errorMessage) => new() { ErrorMessage = errorMessage };
}
