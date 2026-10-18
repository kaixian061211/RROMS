using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Reservations;

public class EditModel : PageModel
{
    private readonly IReservationService _reservations;
    private readonly IWebHostEnvironment _environment;

    public EditModel(IReservationService reservations, IWebHostEnvironment environment)
    {
        _reservations = reservations;
        _environment = environment;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public ReservationInput Input { get; set; } = new();

    /// <summary>The version the page loaded, sent back on save so a change made in between is caught.</summary>
    [BindProperty]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>Set when the reservation can't be changed. The page shows it instead of the form.</summary>
    public string? BlockedMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.TryGetCustomerId(_environment, out var customerId))
            return Unauthorized();

        var result = await _reservations.GetForChangeAsync(customerId, Id);
        if (!result.Succeeded)
        {
            BlockedMessage = result.ErrorMessage;
            return Page();
        }

        Input = ReservationInput.From(result.Reservation!);
        RowVersion = result.Reservation!.RowVersion;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!User.TryGetCustomerId(_environment, out var customerId))
            return Unauthorized();
        if (!ModelState.IsValid)
            return Page();

        var result = await _reservations.ModifyAsync(customerId, Id, Input, RowVersion);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        TempData["StatusMessage"] = "Your reservation has been updated.";
        return RedirectToPage("Index");
    }
}
