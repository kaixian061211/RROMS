namespace RROMS.Web.Models.CustomerHistoryAndPreferences;

/// <summary>One customer's preferences as staff see them, with the customer's name.</summary>
public record PreferenceView(
    int CustomerUserId,
    string CustomerName,
    string? DietaryNeeds,
    string? FavouriteCategories,
    string? SeatingPreferences,
    DateTime UpdatedAt);
