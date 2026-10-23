using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

/// <summary>One payment in full. Staff and administrators only; refunds are administrators only.</summary>
[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class DetailsModel : PageModel
{
    private readonly IPaymentQueries _queries;
    private readonly IPaymentService _payments;

    public DetailsModel(IPaymentQueries queries, IPaymentService payments)
    {
        _queries = queries;
        _payments = payments;
    }

    public PaymentDetails? Payment { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Payment = await _queries.GetDetailsAsync(id);
        return Payment is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostRefundAsync(int id, string? reason)
    {
        // The page is open to staff, but refunds are for administrators only.
        if (!User.IsInRole(PaymentRoles.Admin))
        {
            return Forbid();
        }

        try
        {
            await _payments.RefundAsync(id, User.GetPaymentUserId(), reason);
            TempData["Success"] = "Payment refunded.";
        }
        catch (PaymentException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }
}