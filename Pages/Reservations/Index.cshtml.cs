using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Reservations;

public class IndexModel : PageModel
{
    private readonly IReservationService _reservations;
    private readonly IWebHostEnvironment _environment;

    public IndexModel(IReservationService reservations, IWebHostEnvironment environment)
    {
        _reservations = reservations;
        _environment = environment;
    }

    public List<Reservation> Reservations { get; private set; } = new();

    /// <summary>Set by the Edit and Cancel pages before they redirect here.</summary>
    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.TryGetCustomerId(_environment, out var customerId))
            return Unauthorized();

        Reservations = await _reservations.GetUpcomingAsync(customerId);
        return Page();
    }
}
