using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Services.OrdersAndReservations;

/// <summary>
/// Stand-in menu until the MenuItem table, which belongs to another module, is on main.
/// TODO: replace with a lookup that reads the real MenuItem table, and change the registration in Program.cs.
/// </summary>
public class TemporaryMenuItemLookup : IMenuItemLookup
{
    private static readonly MenuItemInfo[] MenuItems =
    {
        new(1, "Nasi Lemak", 12.90m, IsAvailable: true),
        new(2, "Chicken Rice", 11.50m, IsAvailable: true),
        new(3, "Char Kway Teow", 13.90m, IsAvailable: true),
        new(4, "Mee Goreng Mamak", 12.50m, IsAvailable: true),
        new(5, "Roti Canai (2 pieces)", 4.50m, IsAvailable: true),
        new(6, "Chicken Satay (10 sticks)", 18.00m, IsAvailable: false),
        new(7, "Teh Tarik", 3.80m, IsAvailable: true),
        new(8, "Iced Lemon Tea", 4.20m, IsAvailable: true),
        new(9, "Cendol", 6.50m, IsAvailable: true)
    };

    public Task<IReadOnlyList<MenuItemInfo>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<MenuItemInfo>>(MenuItems);

    public Task<IReadOnlyDictionary<int, MenuItemInfo>> GetByIdsAsync(IEnumerable<int> menuItemIds)
    {
        var ids = menuItemIds.ToHashSet();
        IReadOnlyDictionary<int, MenuItemInfo> found = MenuItems
            .Where(menuItem => ids.Contains(menuItem.MenuItemId))
            .ToDictionary(menuItem => menuItem.MenuItemId);

        return Task.FromResult(found);
    }
}
