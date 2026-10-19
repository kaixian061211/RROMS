using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Reservations;

public class CancelModel : PageModel
{
    private readonly IReservationService _reservations;
    private readonly IWebHostEnvironment _environment;

    public CancelModel(IReservationService reservations, IWebHostEnvironment environment)
    {
        _reservations = reservations;
        _environment = environment;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    /// <summary>The version the page loaded, sent back on cancel so a change made in between is caught.</summary>
    [BindProperty]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Reservation? Reservation { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.TryGetCustomerId(_environment, out var customerId))
            return Unauthorized();

        var result = await _reservations.GetForChangeAsync(customerId, Id);
        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        Reservation = result.Reservation;
        RowVersion = result.Reservation!.RowVersion;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!User.TryGetCustomerId(_environment, out var customerId))
            return Unauthorized();
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Something went wrong. Please reload the page and try again.";
            return Page();
        }

        var result = await _reservations.CancelAsync(customerId, Id, RowVersion);
        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        TempData["StatusMessage"] = "Your reservation has been cancelled.";
        return RedirectToPage("Index");
    }
}
