using RROMS.Web.Models.CustomerHistoryAndPreferences;

namespace RROMS.Web.Services.CustomerHistoryAndPreferences;

public interface ICustomerPreferenceService
{
    // Customer: their own preferences. The page passes the id from the sign-in.
    Task<CustomerPreference?> GetMineAsync(int customerUserId);
    Task<PreferenceResult> SaveAsync(int customerUserId, PreferenceInput input);

    // Staff and admin: read-only
    /// <summary>Every customer's saved preferences, most recently changed first, optionally filtered by a word.</summary>
    Task<List<PreferenceView>> GetForStaffAsync(string? search);
    Task<PreferenceView?> GetOneForStaffAsync(int customerUserId);
    Task<string> GetCustomerNameAsync(int customerUserId);
}
