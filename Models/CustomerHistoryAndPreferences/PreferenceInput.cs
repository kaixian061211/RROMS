using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.CustomerHistoryAndPreferences;

/// <summary>What the customer sends from the preferences form. Every field is optional.</summary>
public class PreferenceInput
{
    public const int MaxDietaryNeedsLength = 200;
    public const int MaxSeatingLength = 100;

    [Display(Name = "Dietary needs and allergies (optional)")]
    [StringLength(MaxDietaryNeedsLength, ErrorMessage = "Please keep this to {1} characters or fewer.")]
    public string? DietaryNeeds { get; set; }

    [Display(Name = "Favourite menu categories (optional)")]
    public List<string> FavouriteCategories { get; set; } = new();

    /// <summary>A dining area name. Empty means no preference.</summary>
    [Display(Name = "Where you like to sit (optional)")]
    [StringLength(MaxSeatingLength)]
    public string? SeatingPreference { get; set; }
}
