using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>One dish on the order form: which menu item and how many. 0 means the customer didn't pick it.</summary>
public class OrderLineInput
{
    public const int MaxQuantity = 20;

    public int MenuItemId { get; set; }

    [Range(0, MaxQuantity, ErrorMessage = "You can order up to {2} of each item.")]
    public int Quantity { get; set; }
}
