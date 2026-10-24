using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

[Authorize]
public class CancelModel : PageModel
{
    private readonly IPaymentQueries _queries;
    private readonly IPaymentService _payments;
    private readonly ILogger<CancelModel> _logger;

    public CancelModel(
        IPaymentQueries queries,
        IPaymentService payments,
        ILogger<CancelModel> logger)
    {
        _queries = queries;
        _payments = payments;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int PaymentId { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? Attempt { get; set; }

    public PaymentDetails Payment { get; private set; } = null!;
    public string Message { get; private set; } = string.Empty;
    public bool Cancelled { get; private set; }

    public bool CanCancel => !Cancelled
        && Payment is not null
        && Payment.Status == PaymentStatus.Pending
        && PaymentMethod.IsOnline(Payment.Method)
        && Payment.RecordedByStaffId is null
        && !string.IsNullOrWhiteSpace(Payment.TransactionRef)
        && Attempt == Payment.CreatedAt.Ticks;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.TryGetPaymentUserId(out var customerId)
            || customerId <= 0)
        {
            return Forbid();
        }

        if (!ModelState.IsValid
            || PaymentId <= 0
            || Attempt is <= 0
            || Attempt > DateTime.MaxValue.Ticks)
        {
            return BadRequest("The checkout link is invalid.");
        }

        try
        {
            if (!await LoadPaymentAsync(customerId))
            {
                return NotFound();
            }

            Message = CurrentStatusMessage();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not load cancellation page for payment {PaymentId}",
                PaymentId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "Payment status could not be loaded. Please try again later.");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!User.TryGetPaymentUserId(out var customerId)
            || customerId <= 0)
        {
            return Forbid();
        }

        if (!ModelState.IsValid
            || PaymentId <= 0
            || Attempt is null or <= 0
            || Attempt > DateTime.MaxValue.Ticks)
        {
            return BadRequest(
                "The checkout link is invalid. Open your order to try again.");
        }

        try
        {
            if (!await LoadPaymentAsync(customerId))
            {
                return NotFound();
            }

            if (!CanCancel)
            {
                Message = CurrentStatusMessage();
                return Page();
            }

            await _payments.CancelOnlineAttemptAsync(
                PaymentId,
                customerId,
                Attempt);

            if (!await LoadPaymentAsync(customerId))
            {
                return NotFound();
            }

            Cancelled = Payment.Status == PaymentStatus.Failed
                && Attempt == Payment.CreatedAt.Ticks;

            if (Cancelled)
            {
                Message =
                    "This checkout has been cancelled. You can choose how to pay again.";
            }
            else
            {
                Message = CanCancel
                    ? "Cancellation is not confirmed. Check your order before trying again."
                    : CurrentStatusMessage();
            }

            return Page();
        }
        catch (PaymentException ex)
        {
            if (Payment is null)
            {
                return BadRequest(ex.Message);
            }

            ModelState.AddModelError(string.Empty, ex.Message);

            Message =
                "Cancellation is not confirmed. Check your order before trying again.";

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not cancel checkout for payment {PaymentId}",
                PaymentId);

            const string message =
                "Cancellation could not be confirmed. Please check your payment status before trying again.";

            if (Payment is null)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    message);
            }

            Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            Message = message;
            return Page();
        }
    }

    private async Task<bool> LoadPaymentAsync(int customerId)
    {
        var payment = await _queries.GetDetailsAsync(PaymentId);

        if (payment is null || payment.CustomerId != customerId)
        {
            return false;
        }

        Payment = payment;
        return true;
    }

    private string CurrentStatusMessage()
    {
        if (Payment.Status == PaymentStatus.Paid)
        {
            return "This order has been paid. Open your orders to view the receipt.";
        }

        if (Payment.Status == PaymentStatus.Refunded)
        {
            return "This payment has been refunded.";
        }

        if (Payment.IsAwaitingCounter)
        {
            return "This order is waiting for payment at the counter.";
        }

        if (Payment.Status == PaymentStatus.Failed)
        {
            return "The last attempt is marked as failed. Open your order to check it before trying again.";
        }

        if (Attempt is null)
        {
            return "Checkout has not been cancelled. Open your order to check its payment status.";
        }

        if (Attempt != Payment.CreatedAt.Ticks)
        {
            return "This link belongs to an earlier checkout. The current attempt has not been cancelled.";
        }

        if (!CanCancel)
        {
            return "This payment is not confirmed yet. Please contact the restaurant to check it before paying again.";
        }

        return "You have returned from Stripe. Checkout is still pending. You can cancel it below.";
    }
}