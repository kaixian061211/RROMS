using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Services.OrdersAndReservations;

/// <summary>
/// The one place ordering reads menu items (names, prices, availability). The MenuItem table belongs
/// to another module, so swapping TemporaryMenuItemLookup for the real one changes nothing else.
/// </summary>
public interface IMenuItemLookup
{
    /// <summary>Every menu item, available or not, in menu order.</summary>
    Task<IReadOnlyList<MenuItemInfo>> GetAllAsync();

    /// <summary>The menu items with these ids, keyed by id. Unknown ids are simply missing.</summary>
    Task<IReadOnlyDictionary<int, MenuItemInfo>> GetByIdsAsync(IEnumerable<int> menuItemIds);
}
