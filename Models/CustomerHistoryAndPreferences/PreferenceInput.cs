using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.CustomerHistoryAndPreferences;

/// <summary>What the "My preferences" form posts. Who is saving comes from the sign-in, never from the form.</summary>
public class PreferenceInput
{
    [StringLength(200, ErrorMessage = "Dietary needs must be 200 characters or fewer.")]
    [Display(Name = "Dietary needs")]
    public string? DietaryNeeds { get; set; }

    /// <summary>The menu categories the customer ticked.</summary>
    public List<string> FavouriteCategories { get; set; } = new();

    [StringLength(100, ErrorMessage = "Seating preference must be 100 characters or fewer.")]
    [Display(Name = "Seating preference")]
    public string? SeatingPreferences { get; set; }
}
