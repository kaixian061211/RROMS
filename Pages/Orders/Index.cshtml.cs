using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Orders;

public class IndexModel : PageModel
{
    private readonly IOrderService _orders;
    private readonly IWebHostEnvironment _environment;

    public IndexModel(IOrderService orders, IWebHostEnvironment environment)
    {
        _orders = orders;
        _environment = environment;
    }

    public List<Order> Orders { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.TryGetCustomerId(_environment, out var customerId))
            return Unauthorized();

        Orders = await _orders.GetForCustomerAsync(customerId);
        return Page();
    }
}
