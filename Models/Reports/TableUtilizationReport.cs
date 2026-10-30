namespace RROMS.Web.Models.Reports;

/// <summary>
/// Estimated booked table-slots divided by available table-slots.
/// One slot is one table on one date at one seating time.
/// </summary>
public sealed record TableUtilizationReport(
    int AvailableSlots,
    int BookedSlots,
    decimal? UtilizationPercent,
    int DuplicateBookings,
    int UnmatchedBookings,
    IReadOnlyList<TableUtilizationRow> Tables,
    string Basis);

public sealed record TableUtilizationRow(
    int TableId,
    string TableNumber,
    string Area,
    int Capacity,
    int AvailableSlots,
    int BookedSlots,
    decimal? UtilizationPercent);
