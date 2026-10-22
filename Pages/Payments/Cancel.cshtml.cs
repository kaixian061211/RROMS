using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

/// <summary>Stripe's "back" link lands here. We close the Stripe page and free the order for a new attempt.</summary>
[Authorize]
public class CancelModel : PageModel
{
    private readonly IPaymentQueries _queries;
    private readonly IPaymentService _payments;

    public CancelModel(IPaymentQueries queries, IPaymentService payments)
    {
        _queries = queries;
        _payments = payments;
    }

    public int? OrderId { get; private set; }
    public string? Problem { get; private set; }

    public async Task<IActionResult> OnGetAsync(int paymentId, long? attempt)
    {
        var customerId = User.GetPaymentUserId();

        var payment = await _queries.GetDetailsAsync(paymentId);
        if (payment is null || payment.CustomerId != customerId)
        {
            return NotFound();
        }

        OrderId = payment.OrderId;

        try
        {
            await _payments.CancelOnlineAttemptAsync(paymentId, customerId, attempt);
        }
        catch (PaymentException ex)
        {
            Problem = ex.Message;
        }

        return Page();
    }
}