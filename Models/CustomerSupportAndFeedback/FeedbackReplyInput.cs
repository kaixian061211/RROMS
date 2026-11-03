using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.CustomerSupportAndFeedback;

/// <summary>What the admin's reply form posts. Which feedback it answers comes from the address.</summary>
public class FeedbackReplyInput
{
    [Required(ErrorMessage = "Please write a reply.")]
    [StringLength(1000, ErrorMessage = "A reply must be 1000 characters or fewer.")]
    [Display(Name = "Reply to the customer")]
    public string? Reply { get; set; }
}
