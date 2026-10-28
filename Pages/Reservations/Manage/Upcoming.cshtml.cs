using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Reservations.Manage;

/// <summary>Every booking from today onwards, so staff can confirm pending ones without going day by day.</summary>
[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class UpcomingModel : PageModel
{
    /// <summary>The status in the address for "All". With no status the page shows pending bookings.</summary>
    public const string AllStatuses = "all";

    /// <summary>The edit page's "from" value that sends staff back here after saving.</summary>
    public const string From = "upcoming";

    private readonly IReservationService _reservations;

    public UpcomingModel(IReservationService reservations)
    {
        _reservations = reservations;
    }

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? Status { get; set; }

    // Not "page": Razor Pages already uses that name in the address for the page itself.
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    /// <summary>The status to show, or null for all of them.</summary>
    public string? StatusFilter => Status == AllStatuses ? null : Status ?? ReservationStatus.Pending;

    public List<IndexModel.Row> Rows { get; private set; } = new();

    public int TotalPages { get; private set; } = 1;

    public int PendingCount { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    /// <summary>Every restaurant table by id, for table numbers (and on forms, the tables to choose from).</summary>
    public IReadOnlyDictionary<int, TableInfo> Tables { get; private set; } = new Dictionary<int, TableInfo>();

    public async Task<IActionResult> OnGetAsync()
    {
        Tables = await _reservations.GetTablesAsync();
        var page = await _reservations.GetUpcomingForStaffAsync(StatusFilter, PageNumber);
        PageNumber = page.Page;
        TotalPages = page.TotalPages;
        Rows = page.Items
            .Select(r => new IndexModel.Row(r, _reservations.GetAllowedStatusChanges(r), _reservations.CanChangeDetails(r)))
            .ToList();
        PendingCount = await _reservations.CountPendingAsync(DateOnly.FromDateTime(DateTime.Today));
        return Page();
    }

    public async Task<IActionResult> OnPostChangeStatusAsync(int id, string newStatus, byte[] rowVersion)
    {
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

        return RedirectToPage(new { status = Status, pageNumber = PageNumber });
    }
}
