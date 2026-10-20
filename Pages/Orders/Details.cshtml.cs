using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Orders;

public class DetailsModel : PageModel
{
    private readonly IOrderService _orders;
    private readonly IWebHostEnvironment _environment;

    public DetailsModel(IOrderService orders, IWebHostEnvironment environment)
    {
        _orders = orders;
        _environment = environment;
    }

    /// <summary>Null when the order doesn't exist or belongs to another customer.</summary>
    public OrderDetails? Details { get; private set; }

    /// <summary>Set by the order page after the order is placed.</summary>
    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!User.TryGetCustomerId(_environment, out var customerId))
            return Unauthorized();

        Details = await _orders.GetByIdAsync(customerId, id);
        return Page();
    }
}
