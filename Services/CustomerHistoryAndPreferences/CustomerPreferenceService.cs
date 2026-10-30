using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Models.CustomerHistoryAndPreferences;
using RROMS.Web.Models.Restaurant;

namespace RROMS.Web.Services.CustomerHistoryAndPreferences;

public class CustomerPreferenceService : ICustomerPreferenceService
{
    /// <summary>The staff list shows this many customers at most; staff search to narrow it down.</summary>
    public const int MaxListedCustomers = 100;

    private const int MaxCategoriesLength = 200;

    private readonly AppDbContext _db;

    public CustomerPreferenceService(AppDbContext db)
    {
        _db = db;
    }

    // AppDbContext picks these up through their configuration classes, so it needs no DbSet for them.
    private DbSet<CustomerPreference> Preferences => _db.Set<CustomerPreference>();

    public async Task<PreferenceOptions> GetOptionsAsync()
    {
        var categories = await _db.Set<MenuCategory>().AsNoTracking()
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => c.Name)
            .ToListAsync();

        var areas = await _db.Set<DiningArea>().AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => a.Name)
            .ToListAsync();

        return new PreferenceOptions(categories, areas);
    }

    public Task<CustomerPreference?> GetAsync(int customerUserId) =>
        Preferences.AsNoTracking().FirstOrDefaultAsync(p => p.CustomerUserId == customerUserId);

    public async Task<PreferenceResult> SaveAsync(int customerUserId, PreferenceInput input)
    {
        var options = await GetOptionsAsync();

        var dietaryNeeds = Clean(input.DietaryNeeds);
        if (dietaryNeeds is { Length: > PreferenceInput.MaxDietaryNeedsLength })
            return PreferenceResult.Failure(
                $"Please keep your dietary needs to {PreferenceInput.MaxDietaryNeedsLength} characters or fewer.");

        // The seating area must be one the restaurant has; the stored name is the restaurant's spelling.
        var seating = Clean(input.SeatingPreference);
        if (seating is not null)
        {
            seating = options.SeatingAreas.FirstOrDefault(a => string.Equals(a, seating, StringComparison.OrdinalIgnoreCase));
            if (seating is null)
                return PreferenceResult.Failure("Please choose one of the seating areas shown.");
        }

        var categories = new List<string>();
        foreach (var chosen in input.FavouriteCategories.Select(Clean).Where(c => c is not null).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var known = options.Categories.FirstOrDefault(c => string.Equals(c, chosen, StringComparison.OrdinalIgnoreCase));
            if (known is null)
                return PreferenceResult.Failure("Please choose menu categories from the list shown.");
            categories.Add(known);
        }

        var storedCategories = CustomerHistoryFormatter.JoinCategories(categories);
        if (storedCategories is { Length: > MaxCategoriesLength })
            return PreferenceResult.Failure("That's too many categories to save. Please choose fewer.");

        var existing = await Preferences.FirstOrDefaultAsync(p => p.CustomerUserId == customerUserId);
        if (existing is null)
        {
            existing = new CustomerPreference { CustomerUserId = customerUserId };
            Preferences.Add(existing);
        }

        existing.DietaryNeeds = dietaryNeeds;
        existing.FavouriteCategories = storedCategories;
        existing.SeatingPreferences = seating;
        existing.UpdatedAt = DateTime.Now;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two saves for a new customer at once (a double tap): the other one created the row first, so update it.
            _db.ChangeTracker.Clear();
            var created = await Preferences.FirstOrDefaultAsync(p => p.CustomerUserId == customerUserId);
            if (created is null)
                throw;

            created.DietaryNeeds = dietaryNeeds;
            created.FavouriteCategories = storedCategories;
            created.SeatingPreferences = seating;
            created.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            existing = created;
        }

        return PreferenceResult.Success(existing);
    }

    public async Task<List<CustomerSummary>> SearchCustomersAsync(string? search)
    {
        var query = _db.Set<UserAccount>().AsNoTracking().Where(u => u.Role == UserRoles.Customer);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.FullName.Contains(term) || u.Email.Contains(term));
        }

        var customers = await query
            .OrderBy(u => u.FullName).ThenBy(u => u.UserId)
            .Take(MaxListedCustomers)
            .Select(u => new { u.UserId, u.FullName, u.Email, u.IsActive })
            .ToListAsync();

        var ids = customers.Select(c => c.UserId).ToList();
        var withPreferences = (await Preferences.AsNoTracking()
                .Where(p => ids.Contains(p.CustomerUserId))
                .Select(p => p.CustomerUserId)
                .ToListAsync())
            .ToHashSet();

        return customers
            .Select(c => new CustomerSummary(c.UserId, c.FullName, c.Email, c.IsActive, withPreferences.Contains(c.UserId)))
            .ToList();
    }

    public async Task<CustomerPreferenceView?> GetForStaffAsync(int customerUserId)
    {
        var customer = await _db.Set<UserAccount>().AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == customerUserId && u.Role == UserRoles.Customer);
        if (customer is null)
            return null;

        var preference = await GetAsync(customerUserId);
        return new CustomerPreferenceView(customer.UserId, customer.FullName, customer.Email, preference);
    }

    /// <summary>Trims the text; null when nothing is left.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
