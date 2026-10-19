using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Reservations.Manage;

// TODO: uncomment when the login module exists. [Authorize] fails with a 500 until authentication is
// configured, so until then User.IsStaff keeps these pages to Development.
// [Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class IndexModel : PageModel
{
    private readonly IReservationService _reservations;
    private readonly IWebHostEnvironment _environment;

    public IndexModel(IReservationService reservations, IWebHostEnvironment environment)
    {
        _reservations = reservations;
        _environment = environment;
    }

    [BindProperty(SupportsGet = true, Name = "date")]
    public DateOnly? Date { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusFilter { get; set; }

    /// <summary>The chosen date, or today when none was chosen.</summary>
    public DateOnly SelectedDate => Date ?? DateOnly.FromDateTime(DateTime.Today);

    public List<Row> Rows { get; private set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    /// <summary>A reservation plus what staff may do with it right now.</summary>
    public record Row(Reservation Reservation, IReadOnlyList<string> NextStatuses, bool CanEdit);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.IsStaff(_environment))
            return Unauthorized();

        var reservations = await _reservations.GetForDateAsync(SelectedDate, StatusFilter);
        Rows = reservations
            .Select(r => new Row(r, _reservations.GetAllowedStatusChanges(r), _reservations.CanChangeDetails(r)))
            .ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostChangeStatusAsync(int id, string newStatus, byte[] rowVersion)
    {
        if (!User.IsStaff(_environment))
            return Unauthorized();

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Something went wrong. Please try again.";
        }
        else
        {
            var result = await _reservations.ChangeStatusAsync(id, newStatus, rowVersion);
            if (result.Succeeded)
                StatusMessage = $"{ReservationFormatter.Reference(id)} is now {ReservationFormatter.StatusLabel(newStatus).ToLowerInvariant()}.";
            else
                ErrorMessage = result.ErrorMessage;
        }

        return RedirectToPage(new { date = ReservationFormatter.InputDate(SelectedDate), status = StatusFilter });
    }
}
