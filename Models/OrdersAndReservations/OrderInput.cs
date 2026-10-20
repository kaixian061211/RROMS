using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>
/// What the customer sends when placing an order: their table, and menu item ids and quantities.
/// There are no prices here on purpose: the service takes them from the menu.
/// </summary>
public class OrderInput
{
    /// <summary>Nullable so "not chosen yet" reaches the service, which explains what's missing.</summary>
    [Display(Name = "Your table")]
    public int? TableId { get; set; }

    public List<OrderLineInput> Items { get; set; } = new();
}
