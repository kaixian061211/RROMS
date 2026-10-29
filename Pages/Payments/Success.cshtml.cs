using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

/// <summary>
/// Displays payment status and verifies the stored session with Stripe.
/// Webhooks still support customers who leave before returning.
/// </summary>
[Authorize]
public class SuccessModel : PageModel
{
    private readonly IPaymentQueries _queries;
    private readonly IPaymentService _payments;
    private readonly ILogger<SuccessModel> _logger;

    public SuccessModel(
        IPaymentQueries queries,
        IPaymentService payments,
        ILogger<SuccessModel> logger)
    {
        _queries = queries;
        _payments = payments;
        _logger = logger;
    }

    public PaymentDetails? Payment { get; private set; }

    public string? StatusMessage { get; private set; }

    public bool ShouldPoll =>
        Payment is
        {
            Status: PaymentStatus.Pending,
            RecordedByStaffId: null
        }
        && PaymentMethod.IsOnline(Payment.Method);

    public bool CanRefreshFromStripe =>
        Payment is not null
        && Payment.Status is PaymentStatus.Pending or PaymentStatus.Failed
        && Payment.RecordedByStaffId is null
        && PaymentMethod.IsOnline(Payment.Method)
        && !string.IsNullOrWhiteSpace(Payment.TransactionRef);

    public async Task<IActionResult> OnGetAsync(int paymentId)
    {
        if (!User.TryGetPaymentUserId(out var customerId)
            || customerId <= 0)
        {
            return Forbid();
        }

        if (paymentId <= 0 || !ModelState.IsValid)
        {
            return BadRequest("Choose a valid payment.");
        }

        try
        {
            if (!await LoadOwnedPaymentAsync(paymentId, customerId))
            {
                return NotFound();
            }

            // Verify once when the customer enters this page.
            if (CanRefreshFromStripe
                && !await CheckWithStripeAsync(paymentId, customerId))
            {
                return NotFound();
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not load payment status for {PaymentId}",
                paymentId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "Payment status could not be loaded. Check its status before paying again.");
        }
    }

    // Manual rechecking uses the same verification flow.
    public Task<IActionResult> OnPostRefreshAsync(int paymentId) =>
        OnGetAsync(paymentId);

    /// <summary>Automatic polling only reads the database.</summary>
    public async Task<IActionResult> OnGetStatusAsync(int paymentId)
    {
        if (!User.TryGetPaymentUserId(out var customerId)
            || customerId <= 0)
        {
            return Forbid();
        }

        if (paymentId <= 0 || !ModelState.IsValid)
        {
            return BadRequest("Choose a valid payment.");
        }

        try
        {
            var payment = await _queries.GetDetailsAsync(paymentId);

            if (payment is null || payment.CustomerId != customerId)
            {
                return NotFound();
            }

            return new JsonResult(new { status = payment.Status });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not poll payment status for {PaymentId}",
                paymentId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "Payment status could not be loaded. Please try again later.");
        }
    }

    private async Task<bool> LoadOwnedPaymentAsync(
        int paymentId,
        int customerId)
    {
        Payment = await _queries.GetDetailsAsync(paymentId);

        return Payment is not null
            && Payment.CustomerId == customerId;
    }

    private async Task<bool> CheckWithStripeAsync(
        int paymentId,
        int customerId)
    {
        try
        {
            await _payments.RefreshOnlineStatusAsync(
                paymentId,
                customerId);
        }
        catch (PaymentException ex)
        {
            StatusMessage = ex.Message;
        }

        // Reload because a webhook may have updated the payment meanwhile.
        return await LoadOwnedPaymentAsync(paymentId, customerId);
    }
}
