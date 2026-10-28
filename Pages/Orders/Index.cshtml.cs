using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Orders;

[Authorize(Roles = PaymentRoles.Customer)]
public class IndexModel : PageModel
{
    private readonly IOrderService _orders;

    public IndexModel(IOrderService orders)
    {
        _orders = orders;
    }

    public List<OrderDetails> Orders { get; private set; } = new();

    /// <summary>Every restaurant table by id, for table numbers (and on forms, the tables to choose from).</summary>
    public IReadOnlyDictionary<int, TableInfo> Tables { get; private set; } = new Dictionary<int, TableInfo>();

    public async Task<IActionResult> OnGetAsync()
    {
        Tables = await _orders.GetTablesAsync();
        Orders = await _orders.GetForCustomerAsync(User.GetPaymentUserId());
        return Page();
    }
}
