using RROMS.Web.Models.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

[Authorize]
public class HistoryModel : PageModel
{
    private readonly IPaymentQueries _queries;

    public HistoryModel(IPaymentQueries queries)
    {
        _queries = queries;
    }

    public List<PaymentDetails> Payments { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Payments = await _queries.GetForCustomerAsync(User.GetPaymentUserId());
    }
}