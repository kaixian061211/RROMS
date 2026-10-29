using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Reservations;

[Authorize(Roles = PaymentRoles.Customer)]
public class ConfirmationModel : PageModel
{
    private readonly IReservationService _reservations;

    public ConfirmationModel(IReservationService reservations)
    {
        _reservations = reservations;
    }

    public Reservation? Reservation { get; private set; }

    /// <summary>Every restaurant table by id, for table numbers (and on forms, the tables to choose from).</summary>
    public IReadOnlyDictionary<int, TableInfo> Tables { get; private set; } = new Dictionary<int, TableInfo>();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Tables = await _reservations.GetTablesAsync();
        Reservation = await _reservations.GetByIdAsync(User.GetPaymentUserId(), id);
        return Page();
    }
}
