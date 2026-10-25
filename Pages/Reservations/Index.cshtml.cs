using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Reservations;

[Authorize(Roles = PaymentRoles.Customer)]
public class IndexModel : PageModel
{
    private readonly IReservationService _reservations;

    public IndexModel(IReservationService reservations)
    {
        _reservations = reservations;
    }

    public List<Reservation> Reservations { get; private set; } = new();

    /// <summary>Set by the Edit and Cancel pages before they redirect here.</summary>
    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Reservations = await _reservations.GetUpcomingAsync(User.GetPaymentUserId());
        return Page();
    }
}
