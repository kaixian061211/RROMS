using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>The total is not stored. It is calculated from the order items.</summary>
public class Order
{
    public int OrderId { get; set; }

    /// <summary>No FK yet: the user table belongs to the login module.</summary>
    public int CustomerUserId { get; set; }

    /// <summary>Set when the order belongs to a table reservation.</summary>
    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = OrderStatus.Pending;

    public DateTime CreatedAt { get; set; }

    public List<OrderItem> Items { get; set; } = new();

    /// <summary>Sum of the item line totals. Not stored, so it always matches the items. Load Items first.</summary>
    public decimal Total => Items.Sum(item => item.LineTotal);

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;
}
