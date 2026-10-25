using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Orders;

[Authorize(Roles = PaymentRoles.Customer)]
public class DetailsModel : PageModel
{
    private readonly IOrderService _orders;

    public DetailsModel(IOrderService orders)
    {
        _orders = orders;
    }

    /// <summary>Null when the order doesn't exist or belongs to another customer.</summary>
    public OrderDetails? Details { get; private set; }

    /// <summary>Set by the order page after the order is placed.</summary>
    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Details = await _orders.GetByIdAsync(User.GetPaymentUserId(), id);
        return Page();
    }
}
