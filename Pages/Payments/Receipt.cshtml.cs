using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;
using RROMS.Web.Services.Payments.Receipts;

namespace RROMS.Web.Pages.Payments;

/// <summary>Downloads the PDF receipt. Customers can only get their own; staff and admins can get any.</summary>
[Authorize]
public class ReceiptModel : PageModel
{
    private readonly IPaymentQueries _queries;
    private readonly IReceiptService _receipts;

    public ReceiptModel(IPaymentQueries queries, IReceiptService receipts)
    {
        _queries = queries;
        _receipts = receipts;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var payment = await _queries.GetDetailsAsync(id);
        if (payment is null)
        {
            return NotFound();
        }

        var isStaff = User.IsInRole(PaymentRoles.Staff) || User.IsInRole(PaymentRoles.Admin);
        if (!isStaff && payment.CustomerId != User.GetPaymentUserId())
        {
            return Forbid();
        }

        if (!payment.HasReceipt)
        {
            return BadRequest("A receipt is only available once the payment has been paid.");
        }

        var pdf = _receipts.Generate(payment);
        return File(pdf, "application/pdf", $"RROMS-{payment.ReceiptNo}.pdf");
    }
}