using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Models.CustomerHistoryAndPreferences;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.CustomerHistoryAndPreferences;

namespace RROMS.Tests.CustomerHistoryAndPreferences;

/// <summary>Stories 15 and 18: a customer keeps their own preferences, and staff can read them but not change them.</summary>
[Trait("Type", "Unit")]
[Trait("Story", "15")]
public class CustomerPreferenceTests : DatabaseTest
{
    // The test database always has one dining area, "Main hall" (TestDatabase.AddTablesOnceAsync).
    private const string Area = "Main hall";

    private CustomerPreferenceService NewPreferenceService() => new(NewContext());

    private async Task AddCategoryAsync(string name)
    {
        var db = NewContext();
        db.Add(new MenuCategory { Name = name });
        await db.SaveChangesAsync();
    }

    private async Task<int> AddAccountAsync(string name, string email, string role)
    {
        var account = new UserAccount
        {
            FullName = name, Email = email, Role = role, PasswordHash = "not-a-real-hash", CreatedAt = DateTime.UtcNow
        };
        var db = NewContext();
        db.Add(account);
        await db.SaveChangesAsync();
        return account.UserId;
    }

    private Task<List<CustomerPreference>> RowsForAsync(int customer) =>
        NewContext().Set<CustomerPreference>().AsNoTracking().Where(p => p.CustomerUserId == customer).ToListAsync();

    [Fact(DisplayName = "TC-15-01 A customer saves preferences for the first time: one row with what they chose")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task SaveFirstTime()
    {
        await AddCategoryAsync("Mains");
        await AddCategoryAsync("Drinks");

        var result = await NewPreferenceService().SaveAsync(Alice, new PreferenceInput
        {
            DietaryNeeds = "  No peanuts  ",
            FavouriteCategories = new() { "Mains", "Drinks" },
            SeatingPreference = Area
        });

        Assert.True(result.Succeeded, result.ErrorMessage);
        var rows = await RowsForAsync(Alice);
        var saved = Assert.Single(rows);
        Assert.Equal("No peanuts", saved.DietaryNeeds);
        Assert.Equal(new[] { "Mains", "Drinks" }, CustomerHistoryFormatter.SplitCategories(saved.FavouriteCategories));
        Assert.Equal(Area, saved.SeatingPreferences);
    }

    [Fact(DisplayName = "TC-15-02 Saving again changes the same row instead of adding another")]
    [Trait("Technique", "State transition")]
    public async Task SaveTwiceKeepsOneRow()
    {
        await NewPreferenceService().SaveAsync(Alice, new PreferenceInput { DietaryNeeds = "Vegetarian", SeatingPreference = Area });

        var second = await NewPreferenceService().SaveAsync(Alice, new PreferenceInput { DietaryNeeds = "Halal" });

        Assert.True(second.Succeeded, second.ErrorMessage);
        var saved = Assert.Single(await RowsForAsync(Alice));
        Assert.Equal("Halal", saved.DietaryNeeds);
        Assert.Null(saved.SeatingPreferences);   // left empty the second time, so it's cleared
    }

    [Fact(DisplayName = "TC-15-03 Everything left empty is allowed and saves nothing in the columns")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task EmptyPreferencesAreAllowed()
    {
        var result = await NewPreferenceService().SaveAsync(Alice, new PreferenceInput { DietaryNeeds = "   " });

        Assert.True(result.Succeeded, result.ErrorMessage);
        var saved = Assert.Single(await RowsForAsync(Alice));
        Assert.Null(saved.DietaryNeeds);
        Assert.Null(saved.FavouriteCategories);
        Assert.Null(saved.SeatingPreferences);
    }

    [Theory(DisplayName = "TC-15-04 Dietary needs: 200 characters are saved, 201 are rejected")]
    [Trait("Technique", "Boundary value analysis")]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public async Task DietaryNeedsLength(int length, bool allowed)
    {
        var result = await NewPreferenceService().SaveAsync(Alice, new PreferenceInput { DietaryNeeds = new string('x', length) });

        Assert.Equal(allowed, result.Succeeded);
        Assert.Equal(allowed ? 1 : 0, (await RowsForAsync(Alice)).Count);
    }

    [Fact(DisplayName = "TC-15-05 A seating area or category the restaurant doesn't have is rejected, and nothing is saved")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task UnknownChoicesAreRejected()
    {
        var badSeating = await NewPreferenceService().SaveAsync(Alice, new PreferenceInput { SeatingPreference = "On the roof" });
        var badCategory = await NewPreferenceService().SaveAsync(Alice, new PreferenceInput { FavouriteCategories = new() { "Moon food" } });

        Assert.False(badSeating.Succeeded);
        Assert.False(badCategory.Succeeded);
        Assert.Empty(await RowsForAsync(Alice));
    }

    [Fact(DisplayName = "TC-15-06 A customer's preferences are only theirs: asking for another customer's finds nothing")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task CustomersDoNotSeeEachOther()
    {
        await NewPreferenceService().SaveAsync(Alice, new PreferenceInput { DietaryNeeds = "No shellfish" });

        Assert.Equal("No shellfish", (await NewPreferenceService().GetAsync(Alice))!.DietaryNeeds);
        Assert.Null(await NewPreferenceService().GetAsync(Bob));
    }

    [Fact(DisplayName = "TC-18-01 Staff search lists customers only, and says who has saved preferences")]
    [Trait("Story", "18")]
    [Trait("Technique", "Decision table")]
    public async Task StaffSearchListsCustomers()
    {
        var withPrefs = await AddAccountAsync("Aina Prefs", "aina.prefs@example.com", UserRoles.Customer);
        var without = await AddAccountAsync("Aina Blank", "aina.blank@example.com", UserRoles.Customer);
        await AddAccountAsync("Aina Staff", "aina.staff@example.com", UserRoles.Staff);
        await NewPreferenceService().SaveAsync(withPrefs, new PreferenceInput { DietaryNeeds = "Vegan" });

        var found = await NewPreferenceService().SearchCustomersAsync("aina");

        Assert.Equal(new[] { without, withPrefs }, found.Select(c => c.UserId).ToArray());   // by name: Blank, Prefs
        Assert.False(found.Single(c => c.UserId == without).HasPreferences);
        Assert.True(found.Single(c => c.UserId == withPrefs).HasPreferences);
    }

    [Fact(DisplayName = "TC-18-02 Staff view one customer's preferences; a staff or unknown id is not found")]
    [Trait("Story", "18")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task StaffReadOneCustomer()
    {
        var customer = await AddAccountAsync("Dewi Customer", "dewi.customer@example.com", UserRoles.Customer);
        var staff = await AddAccountAsync("Dewi Staff", "dewi.staff@example.com", UserRoles.Staff);
        await NewPreferenceService().SaveAsync(customer, new PreferenceInput { DietaryNeeds = "Halal", SeatingPreference = Area });

        var view = await NewPreferenceService().GetForStaffAsync(customer);

        Assert.NotNull(view);
        Assert.Equal("Dewi Customer", view!.FullName);
        Assert.Equal(("Halal", Area), (view.Preference!.DietaryNeeds, view.Preference.SeatingPreferences));
        Assert.Null(await NewPreferenceService().GetForStaffAsync(staff));
        Assert.Null(await NewPreferenceService().GetForStaffAsync(-1));
    }

    [Fact(DisplayName = "TC-18-03 A customer with no saved preferences shows up for staff with none")]
    [Trait("Story", "18")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task StaffSeeNoPreferencesYet()
    {
        var customer = await AddAccountAsync("Faiz Newcomer", "faiz.new@example.com", UserRoles.Customer);

        var view = await NewPreferenceService().GetForStaffAsync(customer);

        Assert.NotNull(view);
        Assert.Null(view!.Preference);
    }
}
