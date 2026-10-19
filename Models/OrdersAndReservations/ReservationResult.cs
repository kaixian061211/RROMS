namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>
/// What a reservation action returns. Expected failures, like a slot that is already taken,
/// carry a message for the page to show instead of throwing an exception.
/// </summary>
public class ReservationResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public Reservation? Reservation { get; init; }

    public static ReservationResult Success(Reservation reservation) =>
        new() { Succeeded = true, Reservation = reservation };

    public static ReservationResult Failure(string errorMessage) =>
        new() { ErrorMessage = errorMessage };
}
