using RROMS.Web.Models.CustomerHistoryAndPreferences;

namespace RROMS.Web.Services.CustomerHistoryAndPreferences;

public interface ICustomerPreferenceService
{
    /// <summary>The menu categories and dining areas the form offers.</summary>
    Task<PreferenceOptions> GetOptionsAsync();

    // Customer actions: only ever about the customer's own preferences

    /// <summary>The customer's saved preferences, or null if they haven't saved any yet.</summary>
    Task<CustomerPreference?> GetAsync(int customerUserId);

    /// <summary>
    /// Saves the customer's preferences, creating them the first time. The seating area and categories must be
    /// ones the restaurant offers, and nothing may be longer than its column.
    /// </summary>
    Task<PreferenceResult> SaveAsync(int customerUserId, PreferenceInput input);

    // Staff actions: read-only, there is deliberately no way for staff to save

    /// <summary>Customers (not staff or admins) whose name or email contains the text, by name. No text lists the first ones.</summary>
    Task<List<CustomerSummary>> SearchCustomersAsync(string? search);

    /// <summary>One customer and their preferences. Null when the id isn't a customer's account.</summary>
    Task<CustomerPreferenceView?> GetForStaffAsync(int customerUserId);
}
