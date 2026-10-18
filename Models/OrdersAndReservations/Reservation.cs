using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.OrdersAndReservations;

public class Reservation
{
    public int ReservationId { get; set; }

    /// <summary>No FK yet: the user table belongs to the login module.</summary>
    public int CustomerUserId { get; set; }

    /// <summary>No FK yet: the RestaurantTable table belongs to another module.</summary>
    public int TableId { get; set; }

    public DateOnly ReservationDate { get; set; }
    public TimeOnly TimeSlot { get; set; }
    public int PartySize { get; set; }

    [StringLength(500)]
    public string? SpecialRequest { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = ReservationStatus.Pending;

    public DateTime CreatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;
}
