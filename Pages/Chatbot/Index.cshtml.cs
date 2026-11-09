using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerSupportAndFeedback;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerSupportAndFeedback;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Chatbot;

[Authorize(Roles = PaymentRoles.Customer)]
public class IndexModel : PageModel
{
    private readonly IChatbotService _chatbot;

    public IndexModel(IChatbotService chatbot)
    {
        _chatbot = chatbot;
    }

    /// <summary>The only thing posted is the question. Who is asking comes from the sign-in.</summary>
    [BindProperty]
    [StringLength(500, ErrorMessage = "Please keep your question to 500 characters or fewer.")]
    public string? Message { get; set; }

    public List<ChatTurn> Conversation { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Conversation = await _chatbot.GetConversationAsync(User.GetPaymentUserId());
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var customerUserId = User.GetPaymentUserId();

        if (ModelState.IsValid)
        {
            var result = await _chatbot.AskAsync(customerUserId, Message);
            if (result.Succeeded)
                return RedirectToPage();   // so refreshing the page doesn't send the question again

            ModelState.AddModelError(nameof(Message), result.ErrorMessage!);
        }

        Conversation = await _chatbot.GetConversationAsync(customerUserId);
        return Page();
    }

    public async Task<IActionResult> OnPostNewAsync()
    {
        await _chatbot.StartNewAsync(User.GetPaymentUserId());
        return RedirectToPage();
    }
}
