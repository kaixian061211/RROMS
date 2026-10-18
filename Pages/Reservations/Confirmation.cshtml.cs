using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Reservations;

public class ConfirmationModel : PageModel
{
    private readonly IReservationService _reservations;
    private readonly IWebHostEnvironment _environment;

    public ConfirmationModel(IReservationService reservations, IWebHostEnvironment environment)
    {
        _reservations = reservations;
        _environment = environment;
    }

    public Reservation? Reservation { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!User.TryGetCustomerId(_environment, out var customerId))
            return Unauthorized();

        Reservation = await _reservations.GetByIdAsync(customerId, id);
        return Page();
    }
}
