using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Reservations.Manage;

// TODO: uncomment when the login module exists (see Manage/Index).
// [Authorize(Roles = PaymentRoles.AdminOrStaff)]
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

    /// <summary>The reservation as loaded, for the customer and status shown above the form.</summary>
    public Reservation? Reservation { get; private set; }

    /// <summary>Set when the reservation can't be changed. The page shows it instead of the form.</summary>
    public string? BlockedMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.IsStaff(_environment))
            return Unauthorized();

        var result = await _reservations.GetForStaffChangeAsync(Id);
        if (!result.Succeeded)
        {
            BlockedMessage = result.ErrorMessage;
            return Page();
        }

        Reservation = result.Reservation!;
        Input = ReservationInput.From(Reservation);
        RowVersion = Reservation.RowVersion;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!User.IsStaff(_environment))
            return Unauthorized();
        if (!ModelState.IsValid)
            return Page();

        var result = await _reservations.ModifyForStaffAsync(Id, Input, RowVersion);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        TempData["StatusMessage"] = $"{ReservationFormatter.Reference(Id)} has been updated.";
        return RedirectToPage("Index", new { date = ReservationFormatter.InputDate(Input.ReservationDate) });
    }
}
