namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>Values stored in Reservation.Status. They must match the CHECK constraint on the table.</summary>
public static class ReservationStatus
{
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";
    public const string Completed = "completed";

    public static readonly string[] All = { Pending, Confirmed, Cancelled, Completed };
}
