namespace RROMS.Web.Models.Reports;

/// <summary>
/// The complete report used by the dashboard and exports.
/// These records are results, not database entities.
/// </summary>
public sealed record DashboardReport(
    ReportPeriod Period,
    string PeriodLabel,
    DateOnly From,
    DateOnly To,                    // Last day, inclusive.
    DateTime GeneratedAt,           // Restaurant-local time.
    SalesReport Sales,
    OrderReport Orders,
    ReservationReport Reservations,
    TableUtilizationReport Utilization,
    PopularMenuReport Menu,
    CustomerActivityReport Customers,
    IReadOnlyList<string> Notes)
{
    public const string Currency = "MYR";
}

/// <summary>A status and its current record count.</summary>
public sealed record StatusCount(string Status, int Count);
