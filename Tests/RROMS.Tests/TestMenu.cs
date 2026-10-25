using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Tests;

/// <summary>A menu the test can change, to check what happens when a price changes or a dish is removed.</summary>
public class TestMenu : IMenuItemLookup
{
    public Dictionary<int, MenuItemInfo> Items { get; } = new();

    public Task<IReadOnlyList<MenuItemInfo>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<MenuItemInfo>>(Items.Values.ToList());

    public Task<IReadOnlyDictionary<int, MenuItemInfo>> GetByIdsAsync(IEnumerable<int> menuItemIds)
    {
        var ids = menuItemIds.ToHashSet();
        return Task.FromResult<IReadOnlyDictionary<int, MenuItemInfo>>(
            Items.Values.Where(m => ids.Contains(m.MenuItemId)).ToDictionary(m => m.MenuItemId));
    }
}
