using RROMS.Web.Models.Payments;

namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>
/// An order with the menu name of each item and its payment (null before one is started), for showing it on a page.
/// The total is Order.Total. CanPay says whether the customer may still go to payment.
/// </summary>
public record OrderDetails(Order Order, IReadOnlyList<OrderLine> Lines, PaymentDetails? Payment, bool CanPay);

/// <summary>One item of an order and its menu name.</summary>
public record OrderLine(OrderItem Item, string MenuItemName);
