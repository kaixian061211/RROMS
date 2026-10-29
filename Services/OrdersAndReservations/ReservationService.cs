using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Services.Notifications;

namespace RROMS.Web.Services.OrdersAndReservations;

public class ReservationService : IReservationService
{
    private const int MaxSpecialRequestLength = 500;
    private const string DoubleBookingIndex = "IX_Reservation_TableId_ReservationDate_TimeSlot";

    private const string NotFoundMessage = "Reservation not found.";
    private const string SlotTakenMessage =
        "Sorry, that table is already booked for this time slot. Please choose another time or table.";
    private const string ChangedByOthersMessage =
        "This reservation was changed by someone else while you were editing it. Please reload the page and try again.";

    // The only status changes allowed: pending -> confirmed -> completed, and pending/confirmed -> cancelled.
    private static readonly Dictionary<string, string[]> AllowedStatusChanges = new()
    {
        [ReservationStatus.Pending] = new[] { ReservationStatus.Confirmed, ReservationStatus.Cancelled },
        [ReservationStatus.Confirmed] = new[] { ReservationStatus.Completed, ReservationStatus.Cancelled }
    };

    private readonly AppDbContext _db;
    private readonly IRestaurantLookup _restaurant;
    private readonly INotificationService _notifications;
    private readonly INotificationDelivery _notificationDelivery;
    private readonly ILogger<ReservationService> _logger;

    public ReservationService(
        AppDbContext db,
        IRestaurantLookup restaurant,
        INotificationService notifications,
        INotificationDelivery notificationDelivery,
        ILogger<ReservationService> logger)
    {
        _db = db;
        _restaurant = restaurant;
        _notifications = notifications;
        _notificationDelivery = notificationDelivery;
        _logger = logger;
    }

    // AppDbContext picks up Reservation through ReservationConfiguration, so it needs no DbSet for it.
    private DbSet<Reservation> Reservations => _db.Set<Reservation>();

    public async Task<bool> IsSlotFreeAsync(int tableId, DateOnly date, TimeOnly timeSlot, int? ignoreReservationId = null)
    {
        var taken = await Reservations.AsNoTracking().AnyAsync(r =>
            r.TableId == tableId &&
            r.ReservationDate == date &&
            r.TimeSlot == timeSlot &&
            r.Status != ReservationStatus.Cancelled &&
            (ignoreReservationId == null || r.ReservationId != ignoreReservationId));

        return !taken;
    }

    public Task<IReadOnlyDictionary<int, TableInfo>> GetTablesAsync() => _restaurant.GetTablesAsync();

    // Customer actions

    public async Task<ReservationResult> CreateAsync(int customerUserId, ReservationInput input)
    {
        // Switching reservations off stops new bookings. Existing ones can still be changed or cancelled.
        if (!await _restaurant.IsAcceptingReservationsAsync())
            return ReservationResult.Failure("Sorry, we're not taking new reservations right now. Please try again later.");

        var error = Validate(input) ?? await CheckTableAndHoursAsync(input);
        if (error is not null)
            return ReservationResult.Failure(error);

        // Validate has checked that the date and party size are filled in.
        var date = input.ReservationDate!.Value;
        var partySize = input.PartySize!.Value;

        if (!await IsSlotFreeAsync(input.TableId, date, input.TimeSlot))
            return ReservationResult.Failure(SlotTakenMessage);

        var reservation = new Reservation
        {
            CustomerUserId = customerUserId,
            TableId = input.TableId,
            ReservationDate = date,
            TimeSlot = input.TimeSlot,
            PartySize = partySize,
            SpecialRequest = TrimToNull(input.SpecialRequest)
        };
        Reservations.Add(reservation);

        var result = await SaveAsync(reservation);
        if (result.Succeeded)
        {
            await NotifyCustomerAsync(
                reservation,
                "Reservation request received",
                $"Your reservation {ReservationFormatter.Reference(reservation.ReservationId)} for {ReservationFormatter.When(reservation.ReservationDate, reservation.TimeSlot)} is pending confirmation.");
        }

        return result;
    }

    public async Task<ReservationResult> ModifyAsync(
        int customerUserId,
        int reservationId,
        ReservationInput input,
        byte[] rowVersion) =>
        await ApplyChangesAsync(
            await FindOwnAsync(customerUserId, reservationId),
            input,
            rowVersion,
            byCustomer: true);

    public async Task<ReservationResult> CancelAsync(int customerUserId, int reservationId, byte[] rowVersion)
    {
        var reservation = await FindOwnAsync(customerUserId, reservationId);
        if (reservation is null)
            return ReservationResult.Failure(NotFoundMessage);

        var error = CheckCanChange(reservation);
        if (error is not null)
            return ReservationResult.Failure(error);

        reservation.Status = ReservationStatus.Cancelled;
        UseLoadedRowVersion(reservation, rowVersion);

        var result = await SaveAsync(reservation);
        if (result.Succeeded)
        {
            await NotifyCustomerAsync(
                reservation,
                "Reservation status updated",
                $"Your reservation {ReservationFormatter.Reference(reservation.ReservationId)} is now cancelled.");
        }

        return result;
    }

    public async Task<List<Reservation>> GetUpcomingAsync(int customerUserId)
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var timeNow = TimeOnly.FromDateTime(now);

        return await Reservations.AsNoTracking()
            .Where(r => r.CustomerUserId == customerUserId)
            .Where(r => r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
            .Where(r => r.ReservationDate > today || (r.ReservationDate == today && r.TimeSlot >= timeNow))
            .OrderBy(r => r.ReservationDate)
            .ThenBy(r => r.TimeSlot)
            .ToListAsync();
    }

    public Task<Reservation?> GetByIdAsync(int customerUserId, int reservationId) =>
        Reservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId && r.CustomerUserId == customerUserId);

    public async Task<ReservationResult> GetForChangeAsync(int customerUserId, int reservationId) =>
        ForChange(await GetByIdAsync(customerUserId, reservationId));

    // Staff actions

    public Task<List<Reservation>> GetForDateAsync(DateOnly date, string? status)
    {
        var query = Reservations.AsNoTracking().Where(r => r.ReservationDate == date);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);

        return query.OrderBy(r => r.TimeSlot).ThenBy(r => r.TableId).ToListAsync();
    }

    public async Task<ReservationPage> GetUpcomingForStaffAsync(string? status, int page)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = Reservations.AsNoTracking().Where(r => r.ReservationDate >= today);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);

        var count = await query.CountAsync();
        var totalPages = Math.Max(1, (count + ReservationPage.PageSize - 1) / ReservationPage.PageSize);
        page = Math.Clamp(page, 1, totalPages);

        // Cancelled bookings can share a table, date and time, so the id keeps the order the same on every page.
        var items = await query
            .OrderBy(r => r.ReservationDate).ThenBy(r => r.TimeSlot).ThenBy(r => r.TableId).ThenBy(r => r.ReservationId)
            .Skip((page - 1) * ReservationPage.PageSize)
            .Take(ReservationPage.PageSize)
            .ToListAsync();

        return new ReservationPage(items, page, totalPages);
    }

    public Task<int> CountPendingAsync(DateOnly from) =>
        Reservations.AsNoTracking().CountAsync(r => r.ReservationDate >= from && r.Status == ReservationStatus.Pending);

    public async Task<ReservationResult> GetForStaffChangeAsync(int reservationId) =>
        ForChange(await Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.ReservationId == reservationId));

    public async Task<ReservationResult> ModifyForStaffAsync(int reservationId, ReservationInput input, byte[] rowVersion) =>
        await ApplyChangesAsync(await Reservations.FindAsync(reservationId), input, rowVersion, byCustomer: false);

    public async Task<ReservationResult> ChangeStatusAsync(
        int reservationId, string newStatus, byte[] rowVersion, string? customerEmail = null)
    {
        var reservation = await Reservations.FindAsync(reservationId);
        if (reservation is null)
            return ReservationResult.Failure(NotFoundMessage);

        var error = CheckStatusChange(reservation, newStatus);
        if (error is not null)
            return ReservationResult.Failure(error);

        reservation.Status = newStatus;
        UseLoadedRowVersion(reservation, rowVersion);

        var result = await SaveAsync(reservation);
        if (result.Succeeded)
        {
            await NotifyCustomerAsync(
                reservation,
                "Reservation status updated",
                $"Your reservation {ReservationFormatter.Reference(reservation.ReservationId)} is now {ReservationFormatter.StatusLabel(newStatus).ToLowerInvariant()}.",
                sendEmail: newStatus == ReservationStatus.Confirmed,
                customerEmail: customerEmail);
        }

        return result;
    }

    public IReadOnlyList<string> GetAllowedStatusChanges(Reservation reservation) =>
        ReservationStatus.All.Where(status => CheckStatusChange(reservation, status) is null).ToList();

    public bool CanChangeDetails(Reservation reservation) => CheckCanChange(reservation) is null;

    // Rules shared by customers and staff

    private async Task<ReservationResult> ApplyChangesAsync(
        Reservation? reservation,
        ReservationInput input,
        byte[] rowVersion,
        bool byCustomer)
    {
        if (reservation is null)
            return ReservationResult.Failure(NotFoundMessage);

        var error = CheckCanChange(reservation) ?? Validate(input) ?? await CheckTableAndHoursAsync(input);
        if (error is not null)
            return ReservationResult.Failure(error);

        // Validate has checked that the date and party size are filled in.
        var date = input.ReservationDate!.Value;
        var partySize = input.PartySize!.Value;

        if (!await IsSlotFreeAsync(input.TableId, date, input.TimeSlot, reservation.ReservationId))
            return ReservationResult.Failure(SlotTakenMessage);

        var previousStatus = reservation.Status;

        // A customer who moves a confirmed booking needs the restaurant to confirm it again.
        if (byCustomer && reservation.Status == ReservationStatus.Confirmed && ChangesBooking(reservation, input))
            reservation.Status = ReservationStatus.Pending;

        reservation.TableId = input.TableId;
        reservation.ReservationDate = date;
        reservation.TimeSlot = input.TimeSlot;
        reservation.PartySize = partySize;
        reservation.SpecialRequest = TrimToNull(input.SpecialRequest);
        UseLoadedRowVersion(reservation, rowVersion);

        var result = await SaveAsync(reservation);
        if (result.Succeeded && byCustomer && reservation.Status != previousStatus)
        {
            await NotifyCustomerAsync(
                reservation,
                "Reservation status updated",
                $"Your reservation {ReservationFormatter.Reference(reservation.ReservationId)} is now pending confirmation.");
        }

        return result;
    }

    private async Task NotifyCustomerAsync(
        Reservation reservation,
        string title,
        string message,
        bool sendEmail = false,
        string? customerEmail = null)
    {
        try
        {
            var notification = await _notifications.StageAsync(new NotificationInput
            {
                UserId = reservation.CustomerUserId,
                Type = NotificationTypes.Reservation,
                Title = title,
                Message = message,
                ReservationId = reservation.ReservationId
            });

            await _db.SaveChangesAsync();
            // One delivery call per event: confirmed reservations also request
            // email, using only the customer address supplied by the caller.
            await _notificationDelivery.DeliverAsync(
                notification.NotificationId,
                sendEmail: sendEmail,
                recipient: sendEmail
                    ? new NotificationEmailRecipient(reservation.CustomerUserId, customerEmail ?? string.Empty)
                    : null);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Reservation {ReservationId} was saved, but its notification could not be staged or delivered.",
                reservation.ReservationId);
        }
    }

    /// <summary>True when the date, time, table or party size would change. The special request doesn't count.</summary>
    private static bool ChangesBooking(Reservation reservation, ReservationInput input) =>
        reservation.TableId != input.TableId ||
        reservation.ReservationDate != input.ReservationDate ||
        reservation.TimeSlot != input.TimeSlot ||
        reservation.PartySize != input.PartySize;

    private static ReservationResult ForChange(Reservation? reservation)
    {
        if (reservation is null)
            return ReservationResult.Failure(NotFoundMessage);

        var error = CheckCanChange(reservation);
        return error is null ? ReservationResult.Success(reservation) : ReservationResult.Failure(error);
    }

    /// <summary>Loads the reservation for updating. Another customer's reservation is treated as not found.</summary>
    private Task<Reservation?> FindOwnAsync(int customerUserId, int reservationId) =>
        Reservations.FirstOrDefaultAsync(r => r.ReservationId == reservationId && r.CustomerUserId == customerUserId);

    private static string? CheckCanChange(Reservation reservation)
    {
        if (reservation.Status == ReservationStatus.Cancelled)
            return "This reservation has already been cancelled.";
        if (reservation.Status == ReservationStatus.Completed)
            return "This reservation is completed, so it can't be changed or cancelled.";
        if (IsInPast(reservation.ReservationDate, reservation.TimeSlot))
            return "This reservation has already passed, so it can't be changed or cancelled.";
        return null;
    }

    private static string? CheckStatusChange(Reservation reservation, string newStatus)
    {
        if (!AllowedStatusChanges.TryGetValue(reservation.Status, out var allowed) || !allowed.Contains(newStatus))
            return $"A {reservation.Status} reservation can't be changed to {newStatus}.";
        if (newStatus == ReservationStatus.Completed && !IsInPast(reservation.ReservationDate, reservation.TimeSlot))
            return "A reservation can only be marked as completed once its time has started.";
        return null;
    }

    /// <summary>The checks that need nothing but the form. The table itself is checked by CheckTableAndHoursAsync.</summary>
    private static string? Validate(ReservationInput input)
    {
        if (input.TableId <= 0)
            return "Please choose a table.";
        if (input.ReservationDate is null)
            return "Please choose a date.";
        if (input.PartySize is null)
            return "Please enter the party size.";
        if (input.PartySize <= 0)
            return "Party size must be at least 1.";
        if (!ReservationTimeSlots.IsValid(input.TimeSlot))
            return "Please choose one of the available time slots.";
        if (IsInPast(input.ReservationDate.Value, input.TimeSlot))
            return "Please choose a date and time in the future.";
        if (input.SpecialRequest?.Length > MaxSpecialRequestLength)
            return $"Special requests can be at most {MaxSpecialRequestLength} characters.";
        return null;
    }

    /// <summary>
    /// The checks against the restaurant's own data: the table must exist, still be in use and seat the party,
    /// and the restaurant must be open at that seating. Runs after Validate, so the date and party size are filled in.
    /// </summary>
    private async Task<string?> CheckTableAndHoursAsync(ReservationInput input)
    {
        var tables = await _restaurant.GetTablesAsync();
        if (!tables.TryGetValue(input.TableId, out var table) || !table.IsActive)
            return "Please choose a table.";
        if (input.PartySize > table.Capacity)
            return $"Table {table.TableNumber} seats {ReservationFormatter.PartySize(table.Capacity)}. Please choose a bigger table.";

        var day = input.ReservationDate!.Value.DayOfWeek;
        var hours = await _restaurant.GetOpeningHoursAsync(day);
        if (hours is { IsClosed: true })
            return $"We're closed on {day}s. Please choose another day.";
        if (hours is { Opens: { } opens, Closes: { } closes } && (input.TimeSlot < opens || input.TimeSlot >= closes))
            return $"On {day}s we're open from {ReservationFormatter.Time(opens)} to {ReservationFormatter.Time(closes)}. Please choose another time.";
        return null;
    }

    private static bool IsInPast(DateOnly date, TimeOnly timeSlot) => date.ToDateTime(timeSlot) < DateTime.Now;

    private static string? TrimToNull(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>
    /// Tells EF which RowVersion the user's page loaded. EF only updates the row if it still has
    /// that RowVersion, so a change saved by someone else in between is caught instead of overwritten.
    /// </summary>
    private void UseLoadedRowVersion(Reservation reservation, byte[] rowVersion)
    {
        _db.Entry(reservation).Property(r => r.RowVersion).OriginalValue = rowVersion;
    }

    private async Task<ReservationResult> SaveAsync(Reservation reservation)
    {
        try
        {
            await _db.SaveChangesAsync();
            return ReservationResult.Success(reservation);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ReservationResult.Failure(ChangedByOthersMessage);
        }
        catch (DbUpdateException ex) when (IsDoubleBooking(ex))
        {
            // Two bookings for the same slot were saved at the same moment. Both passed
            // IsSlotFreeAsync, and the database's unique index rejected the second one.
            return ReservationResult.Failure(SlotTakenMessage);
        }
    }

    // SQL Server error 2601 means a row would break a unique index.
    private static bool IsDoubleBooking(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 } sqlError
        && sqlError.Message.Contains(DoubleBookingIndex);
}
