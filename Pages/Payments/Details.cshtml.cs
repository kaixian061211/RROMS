using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class DetailsModel : PageModel
{
    private readonly IPaymentQueries _queries;
    private readonly IPaymentService _payments;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        IPaymentQueries queries,
        IPaymentService payments,
        ILogger<DetailsModel> logger)
    {
        _queries = queries;
        _payments = payments;
        _logger = logger;
    }

    public PaymentDetails? Payment { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (id <= 0 || !ModelState.IsValid)
        {
            return BadRequest("Choose a valid payment.");
        }

        try
        {
            Payment = await _queries.GetDetailsAsync(id);

            return Payment is null
                ? NotFound()
                : Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not load payment {PaymentId}",
                id);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "Payment details could not be loaded. Please try again later.");
        }
    }

    public async Task<IActionResult> OnPostRefundAsync(int id, string? reason)
    {
        if (!User.IsInRole(PaymentRoles.Staff))
            return Forbid();

        if (!User.TryGetPaymentUserId(out var staffId) || staffId <= 0)
            return Forbid();

        if (id <= 0 || !ModelState.IsValid)
            return BadRequest("The refund request is invalid.");

        try
        {
            await _payments.RefundAsync(id, staffId, reason);
            TempData["Success"] = "Refund recorded.";
        }
        catch (PaymentException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Refund could not be completed for payment {PaymentId}",
                id);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "The refund result could not be confirmed here. Check the payment details and Stripe before trying again.");
        }

        return RedirectToPage(new { id });
    }
}