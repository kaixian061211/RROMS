using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Reservations;

[Authorize(Roles = PaymentRoles.Customer)]
public class CreateModel : PageModel
{
    private readonly IReservationService _reservations;

    public CreateModel(IReservationService reservations)
    {
        _reservations = reservations;
    }

    [BindProperty]
    public ReservationInput Input { get; set; } = new();

    /// <summary>Every restaurant table by id, for table numbers (and on forms, the tables to choose from).</summary>
    public IReadOnlyDictionary<int, TableInfo> Tables { get; private set; } = new Dictionary<int, TableInfo>();

    public async Task<IActionResult> OnGetAsync()
    {
        Tables = await _reservations.GetTablesAsync();
        Input.ReservationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Tables = await _reservations.GetTablesAsync();
        if (!ModelState.IsValid)
            return Page();

        var result = await _reservations.CreateAsync(User.GetPaymentUserId(), Input);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        return RedirectToPage("Confirmation", new { id = result.Reservation!.ReservationId });
    }
}
