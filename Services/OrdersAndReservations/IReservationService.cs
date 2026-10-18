using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Services.OrdersAndReservations;

public interface IReservationService
{
    /// <summary>
    /// True when no active reservation holds this table, date and slot. When editing, pass the
    /// reservation's own id so it doesn't block itself.
    /// </summary>
    Task<bool> IsSlotFreeAsync(int tableId, DateOnly date, TimeOnly timeSlot, int? ignoreReservationId = null);

    Task<ReservationResult> CreateAsync(int customerUserId, ReservationInput input);

    /// <summary>rowVersion is the value the customer's page loaded, so a change saved by someone else in between is caught.</summary>
    Task<ReservationResult> ModifyAsync(int customerUserId, int reservationId, ReservationInput input, byte[] rowVersion);

    Task<ReservationResult> CancelAsync(int customerUserId, int reservationId, byte[] rowVersion);

    /// <summary>The customer's pending and confirmed reservations that haven't started yet, soonest first.</summary>
    Task<List<Reservation>> GetUpcomingAsync(int customerUserId);

    /// <summary>Null when the reservation doesn't exist or belongs to another customer.</summary>
    Task<Reservation?> GetByIdAsync(int customerUserId, int reservationId);

    /// <summary>
    /// The customer's reservation if they may still change or cancel it; otherwise a failure saying
    /// why not (not found, cancelled, completed or already passed).
    /// </summary>
    Task<ReservationResult> GetForChangeAsync(int customerUserId, int reservationId);
}
