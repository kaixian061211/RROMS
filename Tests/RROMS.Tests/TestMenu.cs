using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Tests;

/// <summary>A menu the test can change, to check what happens when a price changes or a dish is removed.</summary>
public class TestMenu : IMenuItemLookup
{
    /// <summary>The menu the tests are written against: 9 dishes, and Chicken Satay (id 6) is sold out.</summary>
    public static readonly MenuItemInfo[] Dishes =
    {
        new(1, "Nasi Lemak", 12.90m, IsAvailable: true, "Mains", 1),
        new(2, "Chicken Rice", 11.50m, IsAvailable: true, "Mains", 1),
        new(3, "Char Kway Teow", 13.90m, IsAvailable: true, "Mains", 1),
        new(4, "Mee Goreng Mamak", 12.50m, IsAvailable: true, "Mains", 1),
        new(5, "Roti Canai (2 pieces)", 4.50m, IsAvailable: true, "Mains", 1),
        new(6, "Chicken Satay (10 sticks)", 18.00m, IsAvailable: false, "Mains", 1),
        new(7, "Teh Tarik", 3.80m, IsAvailable: true, "Drinks", 2),
        new(8, "Iced Lemon Tea", 4.20m, IsAvailable: true, "Drinks", 2),
        new(9, "Cendol", 6.50m, IsAvailable: true, "Desserts", 3)
    };

    public Dictionary<int, MenuItemInfo> Items { get; } = new();

    /// <summary>A menu with the 9 dishes, for the unit tests' order service.</summary>
    public static TestMenu Standard()
    {
        var menu = new TestMenu();
        foreach (var dish in Dishes)
            menu.Items[dish.MenuItemId] = dish;
        return menu;
    }

    /// <summary>
    /// Saves the 9 dishes to the MenuItem table, for the page tests, which run the real app and its real menu.
    /// The database gives them new ids, so this returns each dish's id by name.
    /// </summary>
    public static async Task<Dictionary<string, int>> SaveToDatabaseAsync(AppDbContext db)
    {
        var categories = Dishes
            .Select(d => (d.CategoryName, d.CategoryDisplayOrder))
            .Distinct()
            .ToDictionary(c => c.CategoryName, c => new MenuCategory { Name = c.CategoryName, DisplayOrder = c.CategoryDisplayOrder });
        var items = Dishes
            .Select(d => new MenuItem { Name = d.Name, Price = d.Price, IsAvailable = d.IsAvailable, MenuCategory = categories[d.CategoryName] })
            .ToList();

        db.AddRange(items);
        await db.SaveChangesAsync();
        return items.ToDictionary(item => item.Name, item => item.MenuItemId);
    }

    public Task<IReadOnlyList<MenuItemInfo>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<MenuItemInfo>>(Items.Values.ToList());

    public Task<IReadOnlyDictionary<int, MenuItemInfo>> GetByIdsAsync(IEnumerable<int> menuItemIds)
    {
        var ids = menuItemIds.ToHashSet();
        return Task.FromResult<IReadOnlyDictionary<int, MenuItemInfo>>(
            Items.Values.Where(m => ids.Contains(m.MenuItemId)).ToDictionary(m => m.MenuItemId));
    }
}
