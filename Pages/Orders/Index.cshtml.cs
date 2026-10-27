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

    public async Task<IActionResult> OnGetAsync()
    {
        Orders = await _orders.GetForCustomerAsync(User.GetPaymentUserId());
        return Page();
    }
}
