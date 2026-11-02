using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Reports;
using RROMS.Web.Services.Reports;

namespace RROMS.Tests.Payments;

[Trait("Type", "Unit")]
public class PaymentTimeTests
{
    [Fact]
    public void MalaysiaDisplayAndReportBucketsAgreeAcrossMidnight()
    {
        var utc = new DateTime(2026, 10, 30, 16, 30, 0, DateTimeKind.Unspecified);
        Assert.Equal("31 Oct 2026, 12:30 AM", PaymentFormatter.WhenUtc(utc));
        Assert.Equal(PaymentTime.ToLocal(utc), ReportDateRange.ToLocal(utc));
        var range = ReportDateRange.Create(new ReportFilter(ReportPeriod.Daily, new DateOnly(2026, 10, 31)));
        Assert.True(range.ContainsStored(utc));
    }

    [Fact]
    public void TodaysFilterUsesMalaysiaMidnightInUtc()
    {
        var filter = new PaymentFilter { Preset = DateRangePreset.Today };
        var (start, end) = filter.ResolveRange(new DateTime(2026, 10, 31, 12, 0, 0));
        Assert.Equal(new DateTime(2026, 10, 30, 16, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(start!.Value.AddDays(1), end);
    }

    [Fact]
    public void CustomFilterIncludesTheEntireLastLocalDay()
    {
        var filter = new PaymentFilter { Preset = DateRangePreset.Custom, From = new DateTime(2026, 10, 30), To = new DateTime(2026, 10, 31) };
        var (start, end) = filter.ResolveRange();
        Assert.Equal(new DateTime(2026, 10, 29, 16, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 10, 31, 16, 0, 0, DateTimeKind.Utc), end);
    }
}
