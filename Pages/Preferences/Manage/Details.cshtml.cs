using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerHistoryAndPreferences;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerHistoryAndPreferences;

namespace RROMS.Web.Pages.Preferences.Manage;

/// <summary>
/// One customer for staff: their preferences and their reservations, with any special request on a booking.
/// Orders and payments are deliberately not shown here.
/// </summary>
[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class DetailsModel : PageModel
{
    private readonly ICustomerPreferenceService _preferences;
    private readonly ICustomerHistoryService _history;

    public DetailsModel(ICustomerPreferenceService preferences, ICustomerHistoryService history)
    {
        _preferences = preferences;
        _history = history;
    }

    /// <summary>The customer's id, from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public string CustomerName { get; private set; } = string.Empty;

    /// <summary>Null when the customer hasn't saved any preferences.</summary>
    public PreferenceView? Preference { get; private set; }

    public List<ReservationHistoryItem> Reservations { get; private set; } = new();

    public async Task OnGetAsync()
    {
        CustomerName = await _preferences.GetCustomerNameAsync(Id);
        Preference = await _preferences.GetOneForStaffAsync(Id);
        Reservations = await _history.GetReservationHistoryForStaffAsync(Id);
    }
}
