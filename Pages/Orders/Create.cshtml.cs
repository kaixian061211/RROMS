using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Orders;

[Authorize(Roles = PaymentRoles.Customer)]
public class CreateModel : PageModel
{
    private readonly IOrderService _orders;

    public CreateModel(IOrderService orders)
    {
        _orders = orders;
    }

    /// <summary>Only the table, dish ids and quantities are posted. Prices never come from the browser.</summary>
    [BindProperty]
    public OrderInput Input { get; set; } = new();

    /// <summary>The dishes on the form. Input.Items has one line per dish, in the same order.</summary>
    public IReadOnlyList<MenuItemInfo> Menu { get; private set; } = Array.Empty<MenuItemInfo>();

    public IEnumerable<SelectListItem> TableOptions =>
        TemporaryTables.TableIds.Select(id => new SelectListItem(OrderFormatter.Table(id), id.ToString()));

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadMenuAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            // Only a quantity that isn't a whole number from 0 to 20 gets here, so one message covers it.
            ModelState.AddModelError(string.Empty,
                $"Please enter a quantity from 0 to {OrderLineInput.MaxQuantity} for each dish.");
            await LoadMenuAsync();
            return Page();
        }

        var result = await _orders.CreateAsync(User.GetPaymentUserId(), Input);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            await LoadMenuAsync();
            return Page();
        }

        TempData["StatusMessage"] = "Your order has been placed.";
        return RedirectToPage("Details", new { id = result.Order!.OrderId });
    }

    /// <summary>
    /// Loads the menu and lines Input.Items up with it: one line per dish, keeping any quantity
    /// the customer already chose.
    /// </summary>
    private async Task LoadMenuAsync()
    {
        Menu = await _orders.GetMenuAsync();
        var chosen = Input.Items;
        Input.Items = Menu.Select(dish => new OrderLineInput
        {
            MenuItemId = dish.MenuItemId,
            Quantity = chosen.FirstOrDefault(line => line.MenuItemId == dish.MenuItemId)?.Quantity ?? 0
        }).ToList();
    }
}
