using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerHistoryAndPreferences;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerHistoryAndPreferences;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Preferences;

/// <summary>A customer sees and changes their own preferences. The customer is always the signed-in one.</summary>
[Authorize(Roles = PaymentRoles.Customer)]
public class IndexModel : PageModel
{
    private readonly ICustomerPreferenceService _preferences;

    public IndexModel(ICustomerPreferenceService preferences)
    {
        _preferences = preferences;
    }

    [BindProperty]
    public PreferenceInput Input { get; set; } = new();

    public PreferenceOptions Options { get; private set; } = new(Array.Empty<string>(), Array.Empty<string>());

    /// <summary>Set after a save, before the redirect back to this page.</summary>
    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Options = await _preferences.GetOptionsAsync();

        var saved = await _preferences.GetAsync(User.GetPaymentUserId());
        if (saved is not null)
        {
            Input.DietaryNeeds = saved.DietaryNeeds;
            Input.FavouriteCategories = CustomerHistoryFormatter.SplitCategories(saved.FavouriteCategories);
            Input.SeatingPreference = saved.SeatingPreferences;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Options = await _preferences.GetOptionsAsync();
        if (!ModelState.IsValid)
            return Page();

        var result = await _preferences.SaveAsync(User.GetPaymentUserId(), Input);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        StatusMessage = "Your preferences are saved.";
        return RedirectToPage();
    }
}
