namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>An order with the menu name of each item, for showing it on a page. The total is Order.Total.</summary>
public record OrderDetails(Order Order, IReadOnlyList<OrderLine> Lines);

/// <summary>One item of an order and its menu name.</summary>
public record OrderLine(OrderItem Item, string MenuItemName);
