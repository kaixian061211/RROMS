using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerSupportAndFeedback;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerSupportAndFeedback;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Feedback;

[Authorize(Roles = PaymentRoles.Customer)]
public class IndexModel : PageModel
{
    private readonly IFeedbackService _feedback;

    public IndexModel(IFeedbackService feedback)
    {
        _feedback = feedback;
    }

    public List<FeedbackView> Items { get; private set; } = new();

    /// <summary>How many completed orders and reservations the customer hasn't rated yet.</summary>
    public int ToRateCount { get; private set; }

    /// <summary>Set by the Create, Edit and Delete pages before they redirect here.</summary>
    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        var customerUserId = User.GetPaymentUserId();
        Items = await _feedback.GetMineAsync(customerUserId);
        ToRateCount = (await _feedback.GetRatableAsync(customerUserId)).Count;
    }
}
