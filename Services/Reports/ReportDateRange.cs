using System.Globalization;
using RROMS.Web.Models.Reports;

namespace RROMS.Web.Services.Reports;

/// <summary>A chart interval whose start is restaurant-local time.</summary>
public sealed record ReportBucket(DateTime Start, string Label);

/// <summary>
/// Calculates restaurant-local calendar ranges and their UTC database boundaries.
/// The start is inclusive and the end is exclusive.
/// </summary>
public sealed class ReportDateRange
{
    public const int FirstYear = 2000;
    public const int LastYear = 2100;

    public static readonly TimeZoneInfo Zone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");

    // These conversions assume UTC storage. Verify each timestamp column before querying live data.
    // ReservationDate and TimeSlot remain restaurant-local calendar values.
    public static readonly TimeZoneInfo StoredZone = TimeZoneInfo.Utc;

    private ReportDateRange(
        ReportPeriod period,
        DateOnly from,
        DateOnly toExclusive)
    {
        Period = period;
        From = from;
        ToExclusive = toExclusive;
    }

    public ReportPeriod Period { get; }

    public DateOnly From { get; }

    public DateOnly ToExclusive { get; }

    public int Days => ToExclusive.DayNumber - From.DayNumber;

    /// <summary>The inclusive UTC boundary for timestamp queries.</summary>
    public DateTime FromStored =>
        ToStored(From.ToDateTime(TimeOnly.MinValue));

    /// <summary>The exclusive UTC boundary for timestamp queries.</summary>
    public DateTime ToExclusiveStored =>
        ToStored(ToExclusive.ToDateTime(TimeOnly.MinValue));

    public string Label => Period switch
    {
        ReportPeriod.Daily =>
            From.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture),

        ReportPeriod.Weekly =>
            $"{From.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} to " +
            $"{ToExclusive.AddDays(-1).ToString("d MMM yyyy", CultureInfo.InvariantCulture)}",

        ReportPeriod.Monthly =>
            From.ToString("MMMM yyyy", CultureInfo.InvariantCulture),

        _ => From.Year.ToString(CultureInfo.InvariantCulture)
    };

    public static ReportDateRange Create(ReportFilter filter)
    {
        if (!Enum.IsDefined(filter.Period))
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                "Choose Daily, Weekly, Monthly or Yearly.");
        }

        var date = filter.ReferenceDate;

        if (date.Year < FirstYear || date.Year > LastYear)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                $"Choose a date between {FirstYear} and {LastYear}.");
        }

        switch (filter.Period)
        {
            case ReportPeriod.Daily:
                return new(filter.Period, date, date.AddDays(1));

            case ReportPeriod.Weekly:
                // Monday starts the week; Sunday is six days after Monday.
                var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
                return new(filter.Period, monday, monday.AddDays(7));

            case ReportPeriod.Monthly:
                var first = new DateOnly(date.Year, date.Month, 1);
                return new(filter.Period, first, first.AddMonths(1));

            default:
                return new(
                    filter.Period,
                    new DateOnly(date.Year, 1, 1),
                    new DateOnly(date.Year + 1, 1, 1));
        }
    }

    public bool ContainsStored(DateTime? stored) =>
        stored >= FromStored && stored < ToExclusiveStored;

    /// <summary>
    /// Converts a stored UTC timestamp into restaurant-local time.
    /// SQL datetime values may arrive with an Unspecified Kind.
    /// </summary>
    public static DateTime ToLocal(DateTime stored) =>
        TimeZoneInfo.ConvertTime(
            DateTime.SpecifyKind(stored, DateTimeKind.Unspecified),
            StoredZone,
            Zone);

    /// <summary>Converts restaurant-local time into a UTC timestamp.</summary>
    public static DateTime ToStored(DateTime local) =>
        TimeZoneInfo.ConvertTime(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
            Zone,
            StoredZone);

    public static DateTime NowLocal() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    /// <summary>
    /// Includes empty chart intervals.
    /// Daily timestamp reports use hours; daily reservation reports use one day.
    /// Weekly and monthly reports use days; yearly reports use months.
    /// </summary>
    public IReadOnlyList<ReportBucket> Buckets(bool byTime = true)
    {
        var buckets = new List<ReportBucket>();
        var start = From.ToDateTime(TimeOnly.MinValue);

        if (Period == ReportPeriod.Daily && byTime)
        {
            for (var hour = 0; hour < 24; hour++)
            {
                buckets.Add(new(
                    start.AddHours(hour),
                    $"{hour:00}:00"));
            }
        }
        else if (Period == ReportPeriod.Yearly)
        {
            for (var month = 0; month < 12; month++)
            {
                var monthStart = start.AddMonths(month);

                buckets.Add(new(
                    monthStart,
                    monthStart.ToString(
                        "MMM yyyy",
                        CultureInfo.InvariantCulture)));
            }
        }
        else
        {
            var format = Period == ReportPeriod.Weekly
                ? "ddd d MMM yyyy"
                : "d MMM yyyy";

            for (var day = 0; day < Days; day++)
            {
                var dayStart = start.AddDays(day);

                buckets.Add(new(
                    dayStart,
                    dayStart.ToString(
                        format,
                        CultureInfo.InvariantCulture)));
            }
        }

        return buckets;
    }

    /// <summary>
    /// Finds the chart interval for a restaurant-local date and time.
    /// </summary>
    public DateTime BucketStart(DateTime local, bool byTime = true) =>
        Period switch
        {
            ReportPeriod.Daily when byTime =>
                new DateTime(
                    local.Year,
                    local.Month,
                    local.Day,
                    local.Hour,
                    0,
                    0),

            ReportPeriod.Yearly =>
                new DateTime(local.Year, local.Month, 1),

            _ => local.Date
        };
}
