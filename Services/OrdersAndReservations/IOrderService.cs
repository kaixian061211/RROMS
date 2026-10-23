using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Services.OrdersAndReservations;

public interface IOrderService
{
    /// <summary>The dishes that can be ordered right now, in menu order.</summary>
    Task<IReadOnlyList<MenuItemInfo>> GetMenuAsync();

    /// <summary>
    /// Places an order for the customer's table. Only the table, menu item ids and quantities come from
    /// the customer: each item's price is copied from the menu now, and the total is always calculated from the items.
    /// </summary>
    Task<OrderResult> CreateAsync(int customerUserId, OrderInput input);

    /// <summary>The order with its items and their menu names. Null when it doesn't exist or belongs to another customer.</summary>
    Task<OrderDetails?> GetByIdAsync(int customerUserId, int orderId);

    /// <summary>The customer's orders with their items (so totals can be shown), newest first.</summary>
    Task<List<Order>> GetForCustomerAsync(int customerUserId);

    // Staff actions

    /// <summary>
    /// The staff queue, oldest first. With no status: every order still to deal with (pending to served).
    /// With a status: only those, and for completed or cancelled only today's.
    /// </summary>
    Task<List<OrderQueueItem>> GetQueueAsync(string? status);

    /// <summary>
    /// Moves an order on. Only pending → preparing → ready → served → completed and pending → cancelled
    /// are allowed, and a paid order can't be cancelled. rowVersion is what the staff member's page loaded.
    /// </summary>
    Task<OrderResult> ChangeStatusAsync(int orderId, string newStatus, byte[] rowVersion);
}
