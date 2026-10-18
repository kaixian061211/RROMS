using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Reservations;

public class CreateModel : PageModel
{
    private readonly IReservationService _reservations;
    private readonly IWebHostEnvironment _environment;

    public CreateModel(IReservationService reservations, IWebHostEnvironment environment)
    {
        _reservations = reservations;
        _environment = environment;
    }

    [BindProperty]
    public ReservationInput Input { get; set; } = new();

    public IActionResult OnGet()
    {
        if (!User.TryGetCustomerId(_environment, out _))
            return Unauthorized();

        Input.ReservationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!User.TryGetCustomerId(_environment, out var customerId))
            return Unauthorized();
        if (!ModelState.IsValid)
            return Page();

        var result = await _reservations.CreateAsync(customerId, Input);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        return RedirectToPage("Confirmation", new { id = result.Reservation!.ReservationId });
    }
}
