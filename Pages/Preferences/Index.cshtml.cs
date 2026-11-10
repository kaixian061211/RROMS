using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerHistoryAndPreferences;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerHistoryAndPreferences;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Preferences;

/// <summary>A customer tells the restaurant what they like. Staff can read it, so it can only be changed here.</summary>
[Authorize(Roles = PaymentRoles.Customer)]
public class IndexModel : PageModel
{
    private readonly ICustomerPreferenceService _preferences;
    private readonly IMenuItemLookup _menu;
    private readonly IReservationService _reservations;

    public IndexModel(
        ICustomerPreferenceService preferences,
        IMenuItemLookup menu,
        IReservationService reservations)
    {
        _preferences = preferences;
        _menu = menu;
        _reservations = reservations;
    }

    [BindProperty]
    public PreferenceInput Input { get; set; } = new();

    /// <summary>The menu categories to choose from. Anything the customer picked earlier stays in the list.</summary>
    public IReadOnlyList<string> Categories { get; private set; } = Array.Empty<string>();

    /// <summary>The dining areas to choose from, such as Indoor or Outdoor.</summary>
    public IReadOnlyList<string> Areas { get; private set; } = Array.Empty<string>();

    public DateTime? UpdatedAt { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var saved = await _preferences.GetMineAsync(User.GetPaymentUserId());
        if (saved is not null)
        {
            Input = new PreferenceInput
            {
                DietaryNeeds = saved.DietaryNeeds,
                SeatingPreferences = saved.SeatingPreferences,
                FavouriteCategories = (saved.FavouriteCategories ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList()
            };
            UpdatedAt = saved.UpdatedAt;
        }

        await LoadOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            var result = await _preferences.SaveAsync(User.GetPaymentUserId(), Input);
            if (result.Succeeded)
            {
                TempData["StatusMessage"] = "Your preferences have been saved.";
                return RedirectToPage();
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
        }

        await LoadOptionsAsync();
        return Page();
    }

    private async Task LoadOptionsAsync()
    {
        var menu = await _menu.GetAllAsync();
        var categories = menu
            .OrderBy(item => item.CategoryDisplayOrder)
            .Select(item => item.CategoryName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList();
        foreach (var saved in Input.FavouriteCategories)
            if (!categories.Contains(saved, StringComparer.OrdinalIgnoreCase))
                categories.Add(saved);
        Categories = categories;

        var tables = await _reservations.GetTablesAsync();
        var areas = tables.Values
            .Where(table => table.IsActive)
            .Select(table => table.AreaName)
            .Distinct()
            .OrderBy(name => name)
            .ToList();
        if (!string.IsNullOrWhiteSpace(Input.SeatingPreferences)
            && !areas.Contains(Input.SeatingPreferences, StringComparer.OrdinalIgnoreCase))
            areas.Add(Input.SeatingPreferences);
        Areas = areas;
    }
}
