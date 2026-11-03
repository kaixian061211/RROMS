using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerSupportAndFeedback;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerSupportAndFeedback;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Feedback;

[Authorize(Roles = PaymentRoles.Customer)]
public class EditModel : PageModel
{
    private readonly IFeedbackService _feedback;

    public EditModel(IFeedbackService feedback)
    {
        _feedback = feedback;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public FeedbackEditInput Input { get; set; } = new();

    /// <summary>The feedback being edited. It is null when it isn't this customer's.</summary>
    public FeedbackView? Existing { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Existing is not null)
            Input = new FeedbackEditInput { Rating = Existing.Rating, Comments = Existing.Comments };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (Existing is null || !ModelState.IsValid)
            return Page();

        var result = await _feedback.UpdateOwnAsync(User.GetPaymentUserId(), Id, Input.Rating, Input.Comments);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        TempData["StatusMessage"] = "Your feedback has been updated.";
        return RedirectToPage("Index");
    }

    /// <summary>Looks the feedback up among this customer's own, so someone else's id finds nothing.</summary>
    private async Task LoadAsync()
    {
        var mine = await _feedback.GetMineAsync(User.GetPaymentUserId());
        Existing = mine.FirstOrDefault(f => f.FeedbackId == Id);
        if (Existing is null)
            ErrorMessage = "We couldn't find that feedback.";
    }
}
