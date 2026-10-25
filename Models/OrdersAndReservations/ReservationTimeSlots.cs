namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>
/// The fixed seatings a table can be booked for. Each slot is a whole seating, so bookings in
/// different slots never overlap and the double-booking index only has to compare exact times.
/// </summary>
public static class ReservationTimeSlots
{
    public static readonly TimeOnly[] All =
    {
        new(12, 0), new(13, 30),                // lunch
        new(18, 0), new(19, 30), new(21, 0)     // dinner
    };

    public static bool IsValid(TimeOnly timeSlot) => All.Contains(timeSlot);

    /// <summary>Seatings before 5 pm are lunch; the rest are dinner. The pages show them in these two groups.</summary>
    public static IEnumerable<TimeOnly> Lunch => All.Where(slot => slot.Hour < 17);

    public static IEnumerable<TimeOnly> Dinner => All.Where(slot => slot.Hour >= 17);
}
