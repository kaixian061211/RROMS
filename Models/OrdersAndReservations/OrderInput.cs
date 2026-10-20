using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>
/// What the customer sends when placing an order: menu item ids and quantities, and optionally a
/// reservation. There are no prices here on purpose: the service takes them from the menu.
/// </summary>
public class OrderInput
{
    [Display(Name = "Reservation (optional)")]
    public int? ReservationId { get; set; }

    public List<OrderLineInput> Items { get; set; } = new();
}
