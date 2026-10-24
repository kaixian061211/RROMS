using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class CollectModel : PageModel
{
    private readonly IPaymentQueries _queries;
    private readonly IPaymentService _payments;
    private readonly ILogger<CollectModel> _logger;

    public CollectModel(
        IPaymentQueries queries,
        IPaymentService payments,
        ILogger<CollectModel> logger)
    {
        _queries = queries;
        _payments = payments;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public string Method { get; set; } = PaymentMethod.Cash;

    public PaymentDetails? Payment { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.TryGetPaymentUserId(out var staffId) ||
            staffId <= 0)
        {
            return Forbid();
        }

        if (Id <= 0 || !ModelState.IsValid)
        {
            return BadRequest("Choose a valid payment.");
        }

        try
        {
            Payment = await _queries.GetDetailsAsync(Id);

            if (Payment is null)
            {
                return NotFound();
            }

            if (!Payment.CanCollectAtCounter)
            {
                TempData["Info"] =
                    "That payment is no longer available for collection. Its order may have been cancelled.";

                return RedirectToPage("Counter");
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not load counter payment {PaymentId}",
                Id);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "The payment could not be loaded. Please try again later.");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!User.TryGetPaymentUserId(out var staffId) ||
            staffId <= 0)
        {
            return Forbid();
        }

        if (Id <= 0 ||
            ModelState[nameof(Id)]?.Errors.Count > 0)
        {
            return BadRequest("Choose a valid payment.");
        }

        try
        {
            Payment = await _queries.GetDetailsAsync(Id);

            if (Payment is null)
            {
                return NotFound();
            }

            if (!Payment.CanCollectAtCounter)
            {
                TempData["Info"] =
                    "That payment is no longer available for collection. Its order may have been cancelled.";

                return RedirectToPage("Counter");
            }

            if (!PaymentMethod.CounterMethods.Contains(Method))
            {
                ModelState.AddModelError(
                    nameof(Method),
                    "Choose cash, card or e-wallet.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            await _payments.CollectAtCounterAsync(
                Id,
                staffId,
                Method);

            TempData["Success"] = "Payment recorded.";

            return RedirectToPage(
                "Details",
                new { id = Id });
        }
        catch (PaymentException ex)
        {
            if (Payment is null)
            {
                return BadRequest(ex.Message);
            }

            ModelState.AddModelError(
                string.Empty,
                ex.Message);

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Counter collection could not be confirmed for payment {PaymentId}",
                Id);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "The payment could not be confirmed. Check its current status before recording it again.");
        }
    }
}