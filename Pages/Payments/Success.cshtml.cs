using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

/// <summary>
/// Where Stripe (or the pay-at-the-restaurant option) sends the customer afterwards. This page only
/// DISPLAYS the result - the webhook is what marks a payment as paid.
/// </summary>
[Authorize]
public class SuccessModel : PageModel
{
    private readonly IPaymentQueries _queries;

    public SuccessModel(IPaymentQueries queries)
    {
        _queries = queries;
    }

    public PaymentDetails? Payment { get; private set; }

    /// <summary>An online payment still waiting for Stripe's webhook.</summary>
    public bool ShouldPoll =>
        Payment is { Status: PaymentStatus.Pending, IsAwaitingCounter: false };

    public async Task<IActionResult> OnGetAsync(int paymentId)
    {
        Payment = await _queries.GetDetailsAsync(paymentId);
        if (Payment is null || Payment.CustomerId != User.GetPaymentUserId())
        {
            return NotFound();
        }

        return Page();
    }

    /// <summary>Polled by the page's JavaScript while the webhook is on its way.</summary>
    public async Task<IActionResult> OnGetStatusAsync(int paymentId)
    {
        var payment = await _queries.GetDetailsAsync(paymentId);
        if (payment is null || payment.CustomerId != User.GetPaymentUserId())
        {
            return NotFound();
        }

        return new JsonResult(new { status = payment.Status });
    }
}