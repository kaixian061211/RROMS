using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Services.OrdersAndReservations;

public interface IReservationService
{
    /// <summary>
    /// True when no active reservation holds this table, date and slot. When editing, pass the
    /// reservation's own id so it doesn't block itself.
    /// </summary>
    Task<bool> IsSlotFreeAsync(int tableId, DateOnly date, TimeOnly timeSlot, int? ignoreReservationId = null);

    /// <summary>Every restaurant table by id, retired ones too, for showing table numbers. Forms offer only the active ones.</summary>
    Task<IReadOnlyDictionary<int, TableInfo>> GetTablesAsync();

    // Customer actions

    Task<ReservationResult> CreateAsync(int customerUserId, ReservationInput input);

    /// <summary>
    /// rowVersion is the value the customer's page loaded, so a change saved by someone else in between is caught.
    /// If a confirmed booking's date, time, table or party size changes, it goes back to pending.
    /// </summary>
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

    // Staff actions

    /// <summary>Every reservation on a date, optionally only those with one status, by time then table.</summary>
    Task<List<Reservation>> GetForDateAsync(DateOnly date, string? status);

    /// <summary>
    /// Reservations from today onwards, optionally only those with one status, by date, time then table,
    /// one page at a time. A page number outside the list gives the first or last page.
    /// </summary>
    Task<ReservationPage> GetUpcomingForStaffAsync(string? status, int page);

    /// <summary>How many pending reservations are dated on or after this date: the ones staff still have to confirm.</summary>
    Task<int> CountPendingAsync(DateOnly from);

    /// <summary>Any customer's reservation if its details can still be changed; otherwise a failure saying why not.</summary>
    Task<ReservationResult> GetForStaffChangeAsync(int reservationId);

    /// <summary>Like ModifyAsync, but for any customer's reservation, and the status stays as it is.</summary>
    Task<ReservationResult> ModifyForStaffAsync(int reservationId, ReservationInput input, byte[] rowVersion);

    /// <summary>
    /// Moves a reservation to another status. Only pending → confirmed → completed and
    /// pending/confirmed → cancelled are allowed, and completed only once the booking time has started.
    /// </summary>
    Task<ReservationResult> ChangeStatusAsync(int reservationId, string newStatus, byte[] rowVersion);

    /// <summary>The statuses this reservation may move to right now, so pages only offer those actions.</summary>
    IReadOnlyList<string> GetAllowedStatusChanges(Reservation reservation);

    /// <summary>False for cancelled, completed and past reservations.</summary>
    bool CanChangeDetails(Reservation reservation);
}
