using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Models.CustomerHistoryAndPreferences;
using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Services.CustomerHistoryAndPreferences;

public class CustomerPreferenceService : ICustomerPreferenceService
{
    private const int MaxDietaryLength = 200;
    private const int MaxCategoriesLength = 200;
    private const int MaxSeatingLength = 100;

    private readonly AppDbContext _db;

    public CustomerPreferenceService(AppDbContext db) => _db = db;

    // AppDbContext picks these up through their configuration classes, so it needs no DbSet for them.
    private DbSet<CustomerPreference> Preferences => _db.Set<CustomerPreference>();
    private DbSet<UserAccount> Accounts => _db.Set<UserAccount>();

    // ---------- Customer ----------

    public Task<CustomerPreference?> GetMineAsync(int customerUserId) =>
        Preferences.AsNoTracking().FirstOrDefaultAsync(p => p.CustomerUserId == customerUserId);

    public async Task<PreferenceResult> SaveAsync(int customerUserId, PreferenceInput input)
    {
        var dietary = Clean(input.DietaryNeeds);
        var seating = Clean(input.SeatingPreferences);

        var categories = input.FavouriteCategories
            .Select(category => category.Trim())
            .Where(category => category.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var categoryText = categories.Count == 0 ? null : string.Join(", ", categories);

        if (dietary is not null && dietary.Length > MaxDietaryLength)
            return PreferenceResult.Failure($"Dietary needs must be {MaxDietaryLength} characters or fewer.");
        if (seating is not null && seating.Length > MaxSeatingLength)
            return PreferenceResult.Failure($"Seating preference must be {MaxSeatingLength} characters or fewer.");
        if (categoryText is not null && categoryText.Length > MaxCategoriesLength)
            return PreferenceResult.Failure("You have picked too many favourite categories. Please untick a few.");

        // One row per customer: create it the first time, change it after that
        var preference = await Preferences.FirstOrDefaultAsync(p => p.CustomerUserId == customerUserId);
        if (preference is null)
        {
            preference = new CustomerPreference { CustomerUserId = customerUserId };
            Preferences.Add(preference);
        }

        preference.DietaryNeeds = dietary;
        preference.FavouriteCategories = categoryText;
        preference.SeatingPreferences = seating;
        preference.UpdatedAt = DateTime.Now;   // set here on every save, the database doesn't do it

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The unique index caught two first saves at the same moment
            return PreferenceResult.Failure("Your preferences could not be saved. Please try again.");
        }

        return PreferenceResult.Success();
    }

    // ---------- Staff and admin (the pages must be restricted to staff and admin) ----------

    public async Task<List<PreferenceView>> GetForStaffAsync(string? search)
    {
        var rows = await Preferences.AsNoTracking()
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();

        var names = await NamesAsync(rows.Select(p => p.CustomerUserId));
        var views = rows.Select(p => ToView(p, names)).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            views = views.Where(v => Matches(v, term)).ToList();
        }

        return views;
    }

    public async Task<PreferenceView?> GetOneForStaffAsync(int customerUserId)
    {
        var preference = await Preferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.CustomerUserId == customerUserId);
        if (preference is null) return null;

        var names = await NamesAsync(new[] { customerUserId });
        return ToView(preference, names);
    }

    public async Task<string> GetCustomerNameAsync(int customerUserId)
    {
        var names = await NamesAsync(new[] { customerUserId });
        return names.TryGetValue(customerUserId, out var name) ? name : ReservationFormatter.Customer(customerUserId);
    }

    // ---------- Helpers ----------

    /// <summary>Names from the account table. The Development test customers have no account, so they get "Customer #id".</summary>
    private async Task<Dictionary<int, string>> NamesAsync(IEnumerable<int> customerUserIds)
    {
        var ids = customerUserIds.Distinct().ToList();
        return await Accounts.AsNoTracking()
            .Where(account => ids.Contains(account.UserId))
            .ToDictionaryAsync(account => account.UserId, account => account.FullName);
    }

    private static PreferenceView ToView(CustomerPreference p, IReadOnlyDictionary<int, string> names) =>
        new(p.CustomerUserId,
            names.TryGetValue(p.CustomerUserId, out var name) ? name : ReservationFormatter.Customer(p.CustomerUserId),
            p.DietaryNeeds, p.FavouriteCategories, p.SeatingPreferences, p.UpdatedAt);

    private static bool Matches(PreferenceView view, string term) =>
        Contains(view.CustomerName, term)
        || Contains(view.DietaryNeeds, term)
        || Contains(view.FavouriteCategories, term)
        || Contains(view.SeatingPreferences, term);

    private static bool Contains(string? text, string term) =>
        text is not null && text.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
