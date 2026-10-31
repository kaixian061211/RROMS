namespace RROMS.Web.Models.CustomerHistoryAndPreferences;

/// <summary>What saving preferences returns. A failure carries a message for the page to show.</summary>
public class PreferenceResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }

    public static PreferenceResult Success() => new() { Succeeded = true };

    public static PreferenceResult Failure(string errorMessage) => new() { ErrorMessage = errorMessage };
}
