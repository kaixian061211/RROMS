using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Reservations.Manage;

[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class EditModel : PageModel
{
    private readonly IReservationService _reservations;

    public EditModel(IReservationService reservations)
    {
        _reservations = reservations;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    /// <summary>"upcoming" when staff came from the Upcoming list, so Save and Back go back there.</summary>
    [BindProperty(SupportsGet = true, Name = "from")]
    public string? From { get; set; }

    public bool FromUpcoming => From == UpcomingModel.From;

    [BindProperty]
    public ReservationInput Input { get; set; } = new();

    /// <summary>The version the page loaded, sent back on save so a change made in between is caught.</summary>
    [BindProperty]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>The reservation as loaded, for the customer and status shown above the form.</summary>
    public Reservation? Reservation { get; private set; }

    /// <summary>Set when the reservation can't be changed. The page shows it instead of the form.</summary>
    public string? BlockedMessage { get; private set; }

    /// <summary>Every restaurant table by id, for table numbers (and on forms, the tables to choose from).</summary>
    public IReadOnlyDictionary<int, TableInfo> Tables { get; private set; } = new Dictionary<int, TableInfo>();

    public async Task<IActionResult> OnGetAsync()
    {
        Tables = await _reservations.GetTablesAsync();
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
        Tables = await _reservations.GetTablesAsync();
        if (!ModelState.IsValid)
            return Page();

        var result = await _reservations.ModifyForStaffAsync(Id, Input, RowVersion);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        TempData["StatusMessage"] = $"{ReservationFormatter.Reference(Id)} has been updated.";
        if (FromUpcoming)
            return RedirectToPage("Upcoming");
        return RedirectToPage("Index", new { date = ReservationFormatter.InputDate(result.Reservation!.ReservationDate) });
    }
}
