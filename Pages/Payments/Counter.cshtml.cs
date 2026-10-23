using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

/// <summary>
/// The cashier's queue of orders waiting to be paid at the restaurant.
/// Staff and administrators only.
/// </summary>
[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class CounterModel : PageModel
{
    private readonly IPaymentQueries _queries;
    private readonly IPaymentService _payments;

    public CounterModel(IPaymentQueries queries, IPaymentService payments)
    {
        _queries = queries;
        _payments = payments;
    }

    public List<PaymentDetails> Queue { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Queue = await _queries.GetCounterQueueAsync();
    }

    public async Task<IActionResult> OnPostVoidAsync(int paymentId)
    {
        try
        {
            await _payments.VoidPendingAsync(paymentId, User.GetPaymentUserId());
            TempData["Success"] = "Payment cancelled.";
        }
        catch (PaymentException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage();
    }
}