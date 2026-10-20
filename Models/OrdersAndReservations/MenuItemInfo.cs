namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>
/// What ordering needs to know about a menu item. The MenuItem table belongs to another module,
/// so orders only see menu items through IMenuItemLookup.
/// </summary>
public record MenuItemInfo(int MenuItemId, string Name, decimal Price, bool IsAvailable);
