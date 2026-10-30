using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerHistoryAndPreferences;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerHistoryAndPreferences;

namespace RROMS.Web.Pages.Preferences.Manage;

/// <summary>One customer's preferences for staff. There is no form and no POST handler: staff can only read.</summary>
[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class DetailsModel : PageModel
{
    private readonly ICustomerPreferenceService _preferences;

    public DetailsModel(ICustomerPreferenceService preferences)
    {
        _preferences = preferences;
    }

    public CustomerPreferenceView Customer { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var customer = await _preferences.GetForStaffAsync(id);
        if (customer is null)
            return NotFound();

        Customer = customer;
        return Page();
    }
}
