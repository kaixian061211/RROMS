namespace RROMS.Web.Models.Reports;

/// <summary>
/// Scheduled reservations use ReservationDate.
/// New bookings use their creation timestamps.
/// </summary>
public sealed record ReservationReport(
    int ScheduledReservations,
    int ScheduledGuests,            // Guests on non-cancelled reservations.
    IReadOnlyList<StatusCount> StatusBreakdown,
    int NewBookingsCreated,
    int CancelledScheduled,
    decimal? CancellationPercent,
    IReadOnlyList<ReservationTrendPoint> Trend,
    IReadOnlyList<ReservationSlotSummary> PeakSlots,
    IReadOnlyList<ReservationRow> Reservations,
    string Basis);

public sealed record ReservationTrendPoint(
    string Label,
    int Scheduled,
    int Cancelled);

/// <summary>
/// Non-cancelled reservations at one seating time.
/// Tied busiest slots may all be marked as peak.
/// </summary>
public sealed record ReservationSlotSummary(
    TimeOnly TimeSlot,
    int Reservations,
    int Guests,
    bool IsPeak);

public sealed record ReservationRow(
    int ReservationId,
    DateOnly Date,
    TimeOnly TimeSlot,
    string Table,
    string Area,
    int CustomerUserId,
    int PartySize,
    string Status,
    DateTime CreatedAt,             // Restaurant-local time.
    string? SpecialRequest);
