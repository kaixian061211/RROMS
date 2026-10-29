using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.CustomerSupportAndFeedback;

/// <summary>What the edit form posts: the star rating and the comment. Nothing else comes from the browser.</summary>
public class FeedbackEditInput
{
    [Range(1, 5, ErrorMessage = "Please choose a rating from 1 to 5.")]
    public int Rating { get; set; }

    [StringLength(1000, ErrorMessage = "A comment must be 1000 characters or fewer.")]
    [Display(Name = "Comments (optional)")]
    public string? Comments { get; set; }
}

/// <summary>What the "Rate a visit" form posts: the same, plus which visit is being rated.</summary>
public class FeedbackCreateInput : FeedbackEditInput
{
    /// <summary>The visit being rated, written "Order:12" or "Reservation:7".</summary>
    [Required(ErrorMessage = "Please choose the visit you are rating.")]
    public string? Visit { get; set; }
}
