using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Orders;

public class CreateModel : PageModel
{
    private readonly IOrderService _orders;
    private readonly IWebHostEnvironment _environment;

    public CreateModel(IOrderService orders, IWebHostEnvironment environment)
    {
        _orders = orders;
        _environment = environment;
    }

    /// <summary>Only dish ids, quantities and the reservation are posted. Prices never come from the browser.</summary>
    [BindProperty]
    public OrderInput Input { get; set; } = new();

    /// <summary>The dishes on the form. Input.Items has one line per dish, in the same order.</summary>
    public IReadOnlyList<MenuItemInfo> Menu { get; private set; } = Array.Empty<MenuItemInfo>();

    public List<SelectListItem> ReservationOptions { get; private set; } = new();

    /// <summary>reservationId comes from the "Order food" button on My reservations, to choose it in the dropdown.</summary>
    public async Task<IActionResult> OnGetAsync(int? reservationId)
    {
        if (!User.TryGetCustomerId(_environment, out var customerId))
            return Unauthorized();

        Input.ReservationId = reservationId;
        await LoadFormAsync(customerId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!User.TryGetCustomerId(_environment, out var customerId))
            return Unauthorized();

        if (!ModelState.IsValid)
        {
            // Only a quantity that isn't a whole number from 0 to 20 gets here, so one message covers it.
            ModelState.AddModelError(string.Empty,
                $"Please enter a quantity from 0 to {OrderLineInput.MaxQuantity} for each dish.");
            await LoadFormAsync(customerId);
            return Page();
        }

        var result = await _orders.CreateAsync(customerId, Input);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            await LoadFormAsync(customerId);
            return Page();
        }

        TempData["StatusMessage"] = "Your order has been placed.";
        return RedirectToPage("Details", new { id = result.Order!.OrderId });
    }

    /// <summary>
    /// Loads the menu and the reservations for the dropdown, and lines Input.Items up with the menu:
    /// one line per dish, keeping any quantity the customer already chose.
    /// </summary>
    private async Task LoadFormAsync(int customerId)
    {
        Menu = await _orders.GetMenuAsync();
        var chosen = Input.Items;
        Input.Items = Menu.Select(dish => new OrderLineInput
        {
            MenuItemId = dish.MenuItemId,
            Quantity = chosen.FirstOrDefault(line => line.MenuItemId == dish.MenuItemId)?.Quantity ?? 0
        }).ToList();

        var reservations = await _orders.GetReservationsForOrderingAsync(customerId);
        ReservationOptions = reservations
            .Select(r => new SelectListItem(ReservationFormatter.Summary(r), r.ReservationId.ToString()))
            .ToList();
    }
}
