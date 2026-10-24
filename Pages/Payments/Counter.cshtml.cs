using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class CounterModel : PageModel
{
    private readonly IPaymentQueries _queries;
    private readonly IPaymentService _payments;
    private readonly ILogger<CounterModel> _logger;

    public CounterModel(
        IPaymentQueries queries,
        IPaymentService payments,
        ILogger<CounterModel> logger)
    {
        _queries = queries;
        _payments = payments;
        _logger = logger;
    }

    public List<PaymentDetails> Queue { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.TryGetPaymentUserId(out var staffId) ||
            staffId <= 0)
        {
            return Forbid();
        }

        try
        {
            Queue = await _queries.GetCounterQueueAsync();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not load the counter payment queue");

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "Counter payments could not be loaded. Please try again later.");
        }
    }

    public async Task<IActionResult> OnPostVoidAsync(int paymentId)
    {
        if (!User.TryGetPaymentUserId(out var staffId) ||
            staffId <= 0)
        {
            return Forbid();
        }

        if (paymentId <= 0 || !ModelState.IsValid)
        {
            return BadRequest("Choose a valid payment.");
        }

        try
        {
            await _payments.VoidPendingAsync(
                paymentId,
                staffId);

            TempData["Success"] = "Payment cancelled.";
        }
        catch (PaymentException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Counter cancellation could not be confirmed for payment {PaymentId}",
                paymentId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "Cancellation could not be confirmed. Check the payment's current status before trying again.");
        }

        return RedirectToPage();
    }
}