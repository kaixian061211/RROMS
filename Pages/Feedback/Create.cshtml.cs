using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerSupportAndFeedback;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerSupportAndFeedback;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Feedback;

[Authorize(Roles = PaymentRoles.Customer)]
public class CreateModel : PageModel
{
    private readonly IFeedbackService _feedback;

    public CreateModel(IFeedbackService feedback)
    {
        _feedback = feedback;
    }

    /// <summary>Only the visit, the rating and the comment are posted. Who is rating comes from the sign-in.</summary>
    [BindProperty]
    public FeedbackCreateInput Input { get; set; } = new();

    /// <summary>The completed orders and reservations this customer hasn't rated yet.</summary>
    public IReadOnlyList<RatableItem> Visits { get; private set; } = Array.Empty<RatableItem>();

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadVisitsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadVisitsAsync();
            return Page();
        }

        var (orderId, reservationId) = ParseVisit(Input.Visit);
        var result = await _feedback.SubmitAsync(
            User.GetPaymentUserId(), orderId, reservationId, Input.Rating, Input.Comments);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            await LoadVisitsAsync();
            return Page();
        }

        TempData["StatusMessage"] = "Thank you. Your feedback has been sent.";
        return RedirectToPage("Index");
    }

    private async Task LoadVisitsAsync()
    {
        Visits = await _feedback.GetRatableAsync(User.GetPaymentUserId());
    }

    /// <summary>"Order:12" gives the order id, "Reservation:7" the reservation id. Anything else gives neither.</summary>
    private static (int? OrderId, int? ReservationId) ParseVisit(string? visit)
    {
        var parts = (visit ?? string.Empty).Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var id))
            return (null, null);

        return parts[0] switch
        {
            "Order" => (id, null),
            "Reservation" => (null, id),
            _ => (null, null)
        };
    }
}
