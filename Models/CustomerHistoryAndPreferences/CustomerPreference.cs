using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.CustomerHistoryAndPreferences;

/// <summary>
/// What one customer likes: dietary needs, favourite menu categories and where they like to sit.
/// There is at most one row per customer. Only the customer changes it; staff can only read it.
/// </summary>
public class CustomerPreference
{
    public int CustomerPreferenceId { get; set; }

    /// <summary>The UserAccount.UserId of the customer. No FK, like the other modules' CustomerUserId.</summary>
    public int CustomerUserId { get; set; }

    [StringLength(200)]
    public string? DietaryNeeds { get; set; }

    /// <summary>Menu category names joined with '|' (see PreferenceFormatter). Null when none are chosen.</summary>
    [StringLength(200)]
    public string? FavouriteCategories { get; set; }

    /// <summary>The name of a dining area, such as "Outdoor". Null means no preference.</summary>
    [StringLength(100)]
    public string? SeatingPreferences { get; set; }

    public DateTime UpdatedAt { get; set; }
}
