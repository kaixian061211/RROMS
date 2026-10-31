using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerHistoryAndPreferences;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerHistoryAndPreferences;

namespace RROMS.Web.Pages.Preferences.Manage;

/// <summary>Staff and admin read what customers like. Read-only: only a customer changes their own preferences.</summary>
[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class IndexModel : PageModel
{
    private readonly ICustomerPreferenceService _preferences;

    public IndexModel(ICustomerPreferenceService preferences)
    {
        _preferences = preferences;
    }

    /// <summary>A word to look for in the name, dietary needs, categories or seating. Kept in the address.</summary>
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    public List<PreferenceView> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Items = await _preferences.GetForStaffAsync(Search);
    }
}
