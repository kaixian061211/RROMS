using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerSupportAndFeedback;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerSupportAndFeedback;

namespace RROMS.Web.Pages.Feedback;

/// <summary>
/// Everyone's visible feedback. Customers see first-name style labels; staff and admin see which customer
/// and which visit each one is about. Staff only read it: there is nothing to change on this page.
/// </summary>
[Authorize]
public class AllModel : PageModel
{
    private readonly IFeedbackService _feedback;

    public AllModel(IFeedbackService feedback)
    {
        _feedback = feedback;
    }

    /// <summary>The star rating to show, or null for all. Kept in the address, like the order queue's filter.</summary>
    [BindProperty(SupportsGet = true, Name = "stars")]
    public int? Stars { get; set; }

    public List<FeedbackView> Items { get; private set; } = new();

    public bool IsStaffView { get; private set; }

    public async Task OnGetAsync()
    {
        if (Stars is < 1 or > 5)
            Stars = null;

        IsStaffView = User.IsInRole(PaymentRoles.Staff) || User.IsInRole(PaymentRoles.Admin);
        Items = IsStaffView
            ? await _feedback.GetForStaffAsync(Stars)
            : await _feedback.GetVisibleAsync(Stars);
    }
}
