using RROMS.Web.Models.Reports;
using RROMS.Web.Services.Reports;

namespace RROMS.Tests.Reports;

[Trait("Type", "Unit")]
public class ReportDateRangeTests
{
    private static ReportDateRange Range(
        ReportPeriod period,
        int year,
        int month,
        int day) =>
        ReportDateRange.Create(
            new ReportFilter(period, new DateOnly(year, month, day)));

    [Fact(DisplayName = "TC-RP-01 Daily is the selected date")]
    public void DailyIsTheSelectedDate()
    {
        var range = Range(ReportPeriod.Daily, 2026, 10, 29);

        Assert.Equal(new DateOnly(2026, 10, 29), range.From);
        Assert.Equal(new DateOnly(2026, 10, 30), range.ToExclusive);
        Assert.Equal(1, range.Days);
    }

    [Theory(DisplayName = "TC-RP-02 Weekly is Monday to Sunday")]
    [InlineData(2026, 10, 26)]
    [InlineData(2026, 10, 29)]
    [InlineData(2026, 11, 1)]
    public void WeeklyIsMondayToSunday(int year, int month, int day)
    {
        var range = Range(ReportPeriod.Weekly, year, month, day);

        Assert.Equal(new DateOnly(2026, 10, 26), range.From);
        Assert.Equal(new DateOnly(2026, 11, 2), range.ToExclusive);
        Assert.Equal(7, range.Days);
    }

    [Fact(DisplayName = "TC-RP-03 A week can cross into the next year")]
    public void WeekCrossesTheYear()
    {
        var range = Range(ReportPeriod.Weekly, 2027, 1, 1);

        Assert.Equal(new DateOnly(2026, 12, 28), range.From);
        Assert.Equal(new DateOnly(2027, 1, 4), range.ToExclusive);
    }

    [Theory(DisplayName = "TC-RP-04 Monthly covers the calendar month")]
    [InlineData(2026, 10, 31)]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    public void MonthlyCoversTheMonth(int year, int month, int days)
    {
        var range = Range(ReportPeriod.Monthly, year, month, 15);

        Assert.Equal(new DateOnly(year, month, 1), range.From);
        Assert.Equal(
            new DateOnly(year, month, 1).AddMonths(1),
            range.ToExclusive);

        Assert.Equal(days, range.Days);
        Assert.Equal(days, range.Buckets().Count);
    }

    [Theory(DisplayName = "TC-RP-05 Yearly has twelve monthly buckets")]
    [InlineData(2026, 365)]
    [InlineData(2028, 366)]
    public void YearlyCoversTheYear(int year, int days)
    {
        var range = Range(ReportPeriod.Yearly, year, 6, 15);

        Assert.Equal(new DateOnly(year, 1, 1), range.From);
        Assert.Equal(new DateOnly(year + 1, 1, 1), range.ToExclusive);
        Assert.Equal(days, range.Days);
        Assert.Equal(12, range.Buckets().Count);
    }

    [Fact(DisplayName = "TC-RP-06 Daily uses hourly or single-day buckets")]
    public void DailyBuckets()
    {
        var range = Range(ReportPeriod.Daily, 2026, 10, 29);

        Assert.Equal(24, range.Buckets().Count);
        Assert.Equal("13:00", range.Buckets()[13].Label);
        Assert.Single(range.Buckets(byTime: false));
    }

    [Fact(DisplayName = "TC-RP-07 Start is inclusive and end is exclusive")]
    public void StoredBoundaries()
    {
        var range = Range(ReportPeriod.Daily, 2026, 10, 29);

        var midnight = ReportDateRange.ToStored(
            new DateTime(2026, 10, 29, 0, 0, 0));

        var nextMidnight = ReportDateRange.ToStored(
            new DateTime(2026, 10, 30, 0, 0, 0));

        Assert.Equal(midnight, range.FromStored);
        Assert.Equal(nextMidnight, range.ToExclusiveStored);
        Assert.Equal(
            TimeSpan.FromHours(24),
            range.ToExclusiveStored - range.FromStored);

        Assert.True(range.ContainsStored(midnight));
        Assert.True(range.ContainsStored(nextMidnight.AddSeconds(-1)));
        Assert.False(range.ContainsStored(midnight.AddSeconds(-1)));
        Assert.False(range.ContainsStored(nextMidnight));
        Assert.False(range.ContainsStored(null));
    }

    [Fact(DisplayName = "TC-RP-08 Buckets use restaurant-local time")]
    public void BucketsUseRestaurantTime()
    {
        var range = Range(ReportPeriod.Daily, 2026, 10, 29);

        var stored = ReportDateRange.ToStored(
            new DateTime(2026, 10, 29, 0, 30, 0));

        var local = ReportDateRange.ToLocal(stored);

        Assert.Equal(new DateTime(2026, 10, 29, 0, 30, 0), local);
        Assert.Equal(
            new DateTime(2026, 10, 29, 0, 0, 0),
            range.BucketStart(local));

        Assert.True(range.ContainsStored(stored));
    }

    [Theory(DisplayName = "TC-RP-09 Invalid dates and periods are rejected")]
    [InlineData(1999, 12, 31, ReportPeriod.Daily)]
    [InlineData(2101, 1, 1, ReportPeriod.Yearly)]
    [InlineData(2026, 10, 29, (ReportPeriod)99)]
    public void RejectsInvalidFilters(
        int year,
        int month,
        int day,
        ReportPeriod period)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Range(period, year, month, day));
    }

    [Theory(DisplayName = "TC-RP-10 Supported edge dates work")]
    [InlineData(2000, 1, 1)]
    [InlineData(2100, 12, 31)]
    public void EdgeYearsWork(int year, int month, int day)
    {
        foreach (var period in Enum.GetValues<ReportPeriod>())
        {
            Assert.True(Range(period, year, month, day).Days > 0);
        }
    }

    [Fact(DisplayName = "TC-RP-25 Malaysia reporting days use UTC storage boundaries")]
    public void MalaysiaDayUsesUtcStorageBoundaries()
    {
        var range = Range(ReportPeriod.Daily, 2026, 10, 29);

        var start = new DateTime(
            2026, 10, 28, 16, 0, 0, DateTimeKind.Utc);

        var end = new DateTime(
            2026, 10, 29, 16, 0, 0, DateTimeKind.Utc);

        Assert.Equal(start, range.FromStored);
        Assert.Equal(end, range.ToExclusiveStored);
        Assert.Equal(DateTimeKind.Utc, range.FromStored.Kind);

        Assert.True(range.ContainsStored(start));
        Assert.False(range.ContainsStored(start.AddTicks(-1)));
        Assert.True(range.ContainsStored(end.AddTicks(-1)));
        Assert.False(range.ContainsStored(end));

        var stored = new DateTime(
            2026, 10, 28, 16, 30, 0, DateTimeKind.Utc);

        var local = new DateTime(2026, 10, 29, 0, 30, 0);

        Assert.Equal(local, ReportDateRange.ToLocal(stored));
        Assert.Equal(stored, ReportDateRange.ToStored(local));
    }
}
