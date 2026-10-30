using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerHistoryAndPreferences;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerHistoryAndPreferences;

namespace RROMS.Web.Pages.Preferences.Manage;

/// <summary>Staff find a customer and read what they like. Nothing on this page changes data.</summary>
[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class IndexModel : PageModel
{
    private readonly ICustomerPreferenceService _preferences;

    public IndexModel(ICustomerPreferenceService preferences)
    {
        _preferences = preferences;
    }

    public string? Search { get; private set; }

    public List<CustomerSummary> Customers { get; private set; } = new();

    /// <summary>True when the list was cut short, so staff know to search.</summary>
    public bool IsCapped => Customers.Count >= CustomerPreferenceService.MaxListedCustomers;

    public async Task OnGetAsync(string? q)
    {
        Search = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        Customers = await _preferences.SearchCustomersAsync(Search);
    }
}
