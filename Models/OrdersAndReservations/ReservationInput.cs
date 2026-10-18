namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>What the customer chooses when making or changing a reservation.</summary>
public class ReservationInput
{
    public int TableId { get; set; }
    public DateOnly ReservationDate { get; set; }
    public TimeOnly TimeSlot { get; set; }
    public int PartySize { get; set; }
    public string? SpecialRequest { get; set; }
}
