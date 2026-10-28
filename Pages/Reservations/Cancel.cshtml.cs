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
public class CancelModel : PageModel
{
    private readonly IReservationService _reservations;

    public CancelModel(IReservationService reservations)
    {
        _reservations = reservations;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    /// <summary>The version the page loaded, sent back on cancel so a change made in between is caught.</summary>
    [BindProperty]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Reservation? Reservation { get; private set; }

    public string? ErrorMessage { get; private set; }

    /// <summary>Every restaurant table by id, for table numbers (and on forms, the tables to choose from).</summary>
    public IReadOnlyDictionary<int, TableInfo> Tables { get; private set; } = new Dictionary<int, TableInfo>();

    public async Task<IActionResult> OnGetAsync()
    {
        Tables = await _reservations.GetTablesAsync();
        var result = await _reservations.GetForChangeAsync(User.GetPaymentUserId(), Id);
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
        Tables = await _reservations.GetTablesAsync();
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Something went wrong. Please reload the page and try again.";
            return Page();
        }

        var result = await _reservations.CancelAsync(
            User.GetPaymentUserId(),
            Id,
            RowVersion,
            User.FindFirst(ClaimTypes.Email)?.Value);
        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        TempData["StatusMessage"] = "Your reservation has been cancelled.";
        return RedirectToPage("Index");
    }
}
