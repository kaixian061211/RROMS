using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.CustomerHistoryAndPreferences;

/// <summary>What a customer likes: one row per customer, which they fill in and staff can read.</summary>
public class CustomerPreference
{
    public int CustomerPreferenceId { get; set; }

    /// <summary>No FK yet: the user table belongs to the account module.</summary>
    public int CustomerUserId { get; set; }

    [StringLength(200)]
    public string? DietaryNeeds { get; set; }

    /// <summary>Menu category names, separated by commas.</summary>
    [StringLength(200)]
    public string? FavouriteCategories { get; set; }

    [StringLength(100)]
    public string? SeatingPreferences { get; set; }

    public DateTime UpdatedAt { get; set; }
}
