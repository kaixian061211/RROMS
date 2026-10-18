using Microsoft.EntityFrameworkCore;

namespace RROMS.Web.Models.OrdersAndReservations;

public class OrderItem
{
    public int OrderItemId { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    /// <summary>No FK yet: the MenuItem table belongs to another module.</summary>
    public int MenuItemId { get; set; }

    public int Quantity { get; set; }

    /// <summary>The menu price when the order was placed, so later price changes don't alter old orders.</summary>
    [Precision(10, 2)]
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
