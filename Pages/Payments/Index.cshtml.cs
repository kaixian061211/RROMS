using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

/// <summary>All payments, with filters. Staff and administrators only; exports are administrators only.</summary>
[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class IndexModel : PageModel
{
    private readonly IPaymentQueries _queries;

    public IndexModel(IPaymentQueries queries)
    {
        _queries = queries;
    }

    [BindProperty(SupportsGet = true)]
    public PaymentFilter Filter { get; set; } = new();

    public List<PaymentDetails> Rows { get; private set; } = new();

    public decimal PaidTotal => Rows.Where(r => r.Status == PaymentStatus.Paid).Sum(r => r.Amount);

    public async Task OnGetAsync()
    {
        Rows = await _queries.SearchAsync(Filter);
    }
}