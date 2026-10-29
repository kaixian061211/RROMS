using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Reservations;

[Authorize(Roles = PaymentRoles.Customer)]
public class EditModel : PageModel
{
    private readonly IReservationService _reservations;

    public EditModel(IReservationService reservations)
    {
        _reservations = reservations;
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

    public bool IsConfirmed { get; private set; }

    /// <summary>Every restaurant table by id, for table numbers (and on forms, the tables to choose from).</summary>
    public IReadOnlyDictionary<int, TableInfo> Tables { get; private set; } = new Dictionary<int, TableInfo>();

    public async Task<IActionResult> OnGetAsync()
    {
        Tables = await _reservations.GetTablesAsync();
        var result = await _reservations.GetForChangeAsync(User.GetPaymentUserId(), Id);
        if (!result.Succeeded)
        {
            BlockedMessage = result.ErrorMessage;
            return Page();
        }

        Input = ReservationInput.From(result.Reservation!);
        RowVersion = result.Reservation!.RowVersion;
        IsConfirmed = result.Reservation.Status == ReservationStatus.Confirmed;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Tables = await _reservations.GetTablesAsync();
        if (!ModelState.IsValid)
            return Page();

        var result = await _reservations.ModifyAsync(
            User.GetPaymentUserId(),
            Id,
            Input,
            RowVersion,
            User.FindFirst(ClaimTypes.Email)?.Value);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        TempData["StatusMessage"] = "Your reservation has been updated.";
        return RedirectToPage("Index");
    }
}
