using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerSupportAndFeedback;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerSupportAndFeedback;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Feedback;

[Authorize(Roles = PaymentRoles.Customer)]
public class DeleteModel : PageModel
{
    private readonly IFeedbackService _feedback;

    public DeleteModel(IFeedbackService feedback)
    {
        _feedback = feedback;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    /// <summary>The feedback to delete. It is null when it isn't this customer's.</summary>
    public FeedbackView? Existing { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await _feedback.DeleteOwnAsync(User.GetPaymentUserId(), Id);
        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        TempData["StatusMessage"] = "Your feedback has been deleted.";
        return RedirectToPage("Index");
    }

    private async Task LoadAsync()
    {
        var mine = await _feedback.GetMineAsync(User.GetPaymentUserId());
        Existing = mine.FirstOrDefault(f => f.FeedbackId == Id);
        if (Existing is null)
            ErrorMessage = "We couldn't find that feedback.";
    }
}
