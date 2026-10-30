using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerSupportAndFeedback;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerSupportAndFeedback;

namespace RROMS.Web.Pages.Feedback;

/// <summary>
/// Everyone's feedback, with the star filter. What it shows depends on who is signed in:
/// customers see visible feedback with a generic name, staff also see which customer and which visit
/// (read-only), and an admin also sees hidden feedback and can reply, hide and show it again.
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

    public bool IsAdminView { get; private set; }

    /// <summary>Set by the reply page, and by hiding or showing feedback.</summary>
    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        if (Stars is < 1 or > 5)
            Stars = null;

        IsAdminView = User.IsInRole(PaymentRoles.Admin);
        IsStaffView = IsAdminView || User.IsInRole(PaymentRoles.Staff);

        Items = IsAdminView ? await _feedback.GetAllForAdminAsync(Stars)
              : IsStaffView ? await _feedback.GetForStaffAsync(Stars)
              : await _feedback.GetVisibleAsync(Stars);
    }

    public Task<IActionResult> OnPostHideAsync(int id) => ChangeVisibilityAsync(id, hidden: true);

    public Task<IActionResult> OnPostShowAsync(int id) => ChangeVisibilityAsync(id, hidden: false);

    private async Task<IActionResult> ChangeVisibilityAsync(int id, bool hidden)
    {
        // Every signed-in role can open this page, so the admin check has to be here as well.
        // The service doesn't check roles.
        if (!User.IsInRole(PaymentRoles.Admin))
            return Forbid();

        var result = await _feedback.SetHiddenAsync(id, hidden);
        TempData["StatusMessage"] = result.Succeeded
            ? (hidden ? "That feedback is now hidden from customers." : "That feedback is showing again.")
            : result.ErrorMessage;

        return RedirectToPage(new { stars = Stars });
    }
}
