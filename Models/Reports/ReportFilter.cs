namespace RROMS.Web.Models.Reports;

/// <summary>
/// The selected period and a reference date in the restaurant's local time.
/// </summary>
public sealed record ReportFilter(
    ReportPeriod Period,
    DateOnly ReferenceDate);
