using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerSupportAndFeedback;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerSupportAndFeedback;

namespace RROMS.Web.Pages.Feedback;

/// <summary>An admin writes, or edits, the restaurant's reply to one piece of feedback.</summary>
[Authorize(Roles = PaymentRoles.Admin)]
public class ReplyModel : PageModel
{
    private readonly IFeedbackService _feedback;

    public ReplyModel(IFeedbackService feedback)
    {
        _feedback = feedback;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public FeedbackReplyInput Input { get; set; } = new();

    /// <summary>The feedback being answered. It is null when the id doesn't exist.</summary>
    public FeedbackView? Existing { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Existing is not null)
            Input = new FeedbackReplyInput { Reply = Existing.AdminReply };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (Existing is null || !ModelState.IsValid)
            return Page();

        var result = await _feedback.ReplyAsync(Id, Input.Reply!);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        TempData["StatusMessage"] = "Your reply has been saved.";
        return RedirectToPage("All");
    }

    private async Task LoadAsync()
    {
        var all = await _feedback.GetAllForAdminAsync(null);
        Existing = all.FirstOrDefault(f => f.FeedbackId == Id);
        if (Existing is null)
            ErrorMessage = "We couldn't find that feedback.";
    }
}
