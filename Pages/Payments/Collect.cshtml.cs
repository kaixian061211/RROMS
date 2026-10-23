using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

/// <summary>Staff record the money they took at the restaurant. Staff and administrators only.</summary>
[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class CollectModel : PageModel
{
    private readonly IPaymentQueries _queries;
    private readonly IPaymentService _payments;

    public CollectModel(IPaymentQueries queries, IPaymentService payments)
    {
        _queries = queries;
        _payments = payments;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public string Method { get; set; } = PaymentMethod.Cash;

    public PaymentDetails? Payment { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Payment = await _queries.GetDetailsAsync(Id);
        if (Payment is null)
        {
            return NotFound();
        }

        if (!Payment.IsAwaitingCounter)
        {
            TempData["Info"] = "That payment is no longer waiting to be collected.";
            return RedirectToPage("Counter");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            await _payments.CollectAtCounterAsync(Id, User.GetPaymentUserId(), Method);
        }
        catch (PaymentException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToPage(new { id = Id });
        }

        TempData["Success"] = "Payment recorded. The customer has been notified.";
        return RedirectToPage("Details", new { id = Id });
    }
}