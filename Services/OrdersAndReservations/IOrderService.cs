using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Services.OrdersAndReservations;

public interface IOrderService
{
    /// <summary>The dishes that can be ordered right now, in menu order.</summary>
    Task<IReadOnlyList<MenuItemInfo>> GetMenuAsync();

    /// <summary>
    /// Places an order. Only menu item ids and quantities come from the customer: each item's price is
    /// copied from the menu now, and the total is always calculated from the items.
    /// </summary>
    Task<OrderResult> CreateAsync(int customerUserId, OrderInput input);

    /// <summary>The order with its items and their menu names. Null when it doesn't exist or belongs to another customer.</summary>
    Task<OrderDetails?> GetByIdAsync(int customerUserId, int orderId);

    /// <summary>The customer's orders with their items (so totals can be shown), newest first.</summary>
    Task<List<Order>> GetForCustomerAsync(int customerUserId);

    /// <summary>The customer's reservations an order can be linked to: pending or confirmed, today or later.</summary>
    Task<List<Reservation>> GetReservationsForOrderingAsync(int customerUserId);
}
