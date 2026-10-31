using System.Globalization;
using System.Text.Json;
using AngleSharp.Dom;
using RROMS.Tests.Integration;
using RROMS.Web.Models.Dev;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.Reports;

namespace RROMS.Tests.Reports;

[Trait("Type", "Integration")]
public class ReservationReportPageTests : PageTest
{
    private static readonly DateOnly ReportDate = new(2026, 10, 29);
    private const string DailyUrl = "/Reports?Period=Daily&ReferenceDate=2026-10-29";

    public ReservationReportPageTests(TestWebApp app) : base(app) { }

    protected override TestUser? SignedInAs => TestUsers.Admin;

    private static Reservation Booking(
        TimeOnly slot,
        string status = ReservationStatus.Confirmed,
        int table = 1,
        int guests = 2) => new()
        {
            CustomerUserId = TestCustomer,
            TableId = table,
            ReservationDate = ReportDate,
            TimeSlot = slot,
            PartySize = guests,
            Status = status,
            CreatedAt = ReportDateRange.ToStored(new DateTime(2026, 10, 29, 10, 0, 0))
        };

    private Task SaveBookingsAsync(params Reservation[] bookings) =>
        WithDatabaseAsync(async db =>
        {
            db.AddRange(bookings);
            await db.SaveChangesAsync();
        });

    private static IElement Table(IElement section, string caption) =>
        section.QuerySelectorAll("table").Single(table =>
            table.QuerySelector("caption")!.TextContent.Contains(caption));

    [Fact]
    public async Task ReservationSummariesAndChartTotalsMatchScheduledRecords()
    {
        await SaveBookingsAsync(
            Booking(new TimeOnly(12, 0), ReservationStatus.Pending, guests: 3),
            Booking(new TimeOnly(13, 30), ReservationStatus.Confirmed, guests: 4),
            Booking(new TimeOnly(18, 0), ReservationStatus.Completed, guests: 5),
            Booking(new TimeOnly(19, 30), ReservationStatus.Cancelled, guests: 8));

        var page = await GetPageAsync(DailyUrl);
        var section = page.QuerySelector("#reservations")!;
        Assert.Equal(
            new[] { "4", "12", "4", "1", "25.0%" },
            section.QuerySelectorAll("dl dd").Select(TextOf));
        Assert.NotNull(section.QuerySelector("#reservations-trend-chart"));
        Assert.NotNull(section.QuerySelector("#reservations-status-chart"));
        Assert.NotNull(section.QuerySelector("#reservations-peak-chart"));
        Assert.Equal(4, section.QuerySelectorAll(".table-hover tbody tr").Length);
        Assert.Contains("Cancelled", TextOf(section));
        using var json = JsonDocument.Parse(page.QuerySelector("#report-data")!.TextContent);
        var data = json.RootElement.GetProperty("reservations");
        Assert.Equal(4, data.GetProperty("trend").GetProperty("scheduled")[0].GetInt32());
        Assert.Equal(1, data.GetProperty("trend").GetProperty("cancelled")[0].GetInt32());
        Assert.Equal(4, data.GetProperty("status").GetProperty("counts").EnumerateArray().Sum(v => v.GetInt32()));
        Assert.Equal(3, data.GetProperty("peak").GetProperty("counts").EnumerateArray().Sum(v => v.GetInt32()));
        Assert.NotNull(page.QuerySelector("#sales"));
        Assert.NotNull(page.QuerySelector("#orders"));
        Assert.True(json.RootElement.TryGetProperty("sales", out _));
        Assert.True(json.RootElement.TryGetProperty("orders", out _));
    }

    [Fact]
    public async Task FullSeatingTimesAndTiedPeaksRemainVisible()
    {
        await SaveBookingsAsync(
            Booking(new TimeOnly(12, 0), table: 1),
            Booking(new TimeOnly(12, 0), table: 2),
            Booking(new TimeOnly(13, 30), table: 1),
            Booking(new TimeOnly(13, 30), table: 2));

        var page = await GetPageAsync(DailyUrl);
        var section = page.QuerySelector("#reservations")!;
        var slotRows = Table(section, "Reserved dining times").QuerySelectorAll("tbody tr");
        Assert.Equal(
            new[] { "12:00 PM", "1:30 PM", "6:00 PM", "7:30 PM", "9:00 PM" },
            slotRows.Select(row => TextOf(row.QuerySelector("th")!)));
        Assert.Equal(2, slotRows.Count(row => row.QuerySelector(".rr-status--good") is not null));
        Assert.All(slotRows.Take(2), row => Assert.Contains("Peak", TextOf(row)));
        using var json = JsonDocument.Parse(page.QuerySelector("#report-data")!.TextContent);
        var peak = json.RootElement.GetProperty("reservations").GetProperty("peak");
        Assert.Equal("1:30 PM", peak.GetProperty("labels")[1].GetString());
        Assert.Equal(new[] { 2, 2, 0, 0, 0 }, peak.GetProperty("counts").EnumerateArray().Select(v => v.GetInt32()));
        Assert.Equal(new[] { true, true, false, false, false }, peak.GetProperty("isPeak").EnumerateArray().Select(v => v.GetBoolean()));
    }

    [Fact]
    public async Task CancelledOnlyPeriodHasNoPeakBookings()
    {
        await SaveBookingsAsync(
            Booking(new TimeOnly(12, 0), ReservationStatus.Cancelled),
            Booking(new TimeOnly(13, 30), ReservationStatus.Cancelled));

        var page = await GetPageAsync(DailyUrl);
        var section = page.QuerySelector("#reservations")!;
        Assert.Equal(
            new[] { "2", "0", "2", "2", "100.0%" },
            section.QuerySelectorAll("dl dd").Select(TextOf));
        Assert.NotNull(section.QuerySelector("#reservations-trend-chart"));
        Assert.NotNull(section.QuerySelector("#reservations-status-chart"));
        Assert.Null(section.QuerySelector("#reservations-peak-chart"));
        Assert.Contains("No non-cancelled reservations for this period.", TextOf(section));
        Assert.Equal(2, section.QuerySelectorAll(".table-hover tbody tr").Length);
        using var json = JsonDocument.Parse(page.QuerySelector("#report-data")!.TextContent);
        var data = json.RootElement.GetProperty("reservations");
        Assert.Equal(2, data.GetProperty("trend").GetProperty("scheduled")[0].GetInt32());
        Assert.Equal(2, data.GetProperty("trend").GetProperty("cancelled")[0].GetInt32());
        Assert.All(data.GetProperty("peak").GetProperty("counts").EnumerateArray(), v => Assert.Equal(0, v.GetInt32()));
        Assert.All(data.GetProperty("peak").GetProperty("isPeak").EnumerateArray(), v => Assert.False(v.GetBoolean()));
    }

    [Fact]
    public async Task EmptyReservationPeriodRetainsConfiguredSlotsAndOneDailyTrendRow()
    {
        var page = await GetPageAsync(DailyUrl);
        var section = page.QuerySelector("#reservations")!;
        Assert.Empty(section.QuerySelectorAll("canvas"));
        Assert.Contains("No scheduled reservations for this period.", TextOf(section));
        Assert.Equal(
            new[] { "0", "0", "0", "0", "N/A" },
            section.QuerySelectorAll("dl dd").Select(TextOf));
        Assert.Single(Table(section, "Scheduled reservation trend").QuerySelectorAll("tbody tr"));
        Assert.Equal(5, Table(section, "Reserved dining times").QuerySelectorAll("tbody tr").Length);
        Assert.Empty(section.QuerySelectorAll(".rr-status--good"));
        using var json = JsonDocument.Parse(page.QuerySelector("#report-data")!.TextContent);
        var data = json.RootElement.GetProperty("reservations");
        Assert.Equal(1, data.GetProperty("trend").GetProperty("labels").GetArrayLength());
        Assert.Equal(5, data.GetProperty("peak").GetProperty("labels").GetArrayLength());
    }

    [Fact]
    public async Task ScheduledAndCreationDatesStaySeparateAndSpecialRequestsAreEncoded()
    {
        const string note = "</td><script>alert('test')</script><img src=x onerror=alert(1)>";
        var scheduled = Booking(new TimeOnly(13, 30));
        scheduled.CreatedAt = ReportDateRange.ToStored(new DateTime(2026, 9, 30, 12, 30, 0));
        scheduled.SpecialRequest = note;
        var newlyCreated = Booking(new TimeOnly(12, 0));
        newlyCreated.CustomerUserId = OtherCustomer;
        newlyCreated.ReservationDate = new DateOnly(2026, 11, 2);
        await SaveBookingsAsync(scheduled, newlyCreated);

        var page = await GetPageAsync(DailyUrl);
        var section = page.QuerySelector("#reservations")!;
        Assert.Equal(
            new[] { "1", "2", "1", "0", "0.0%" },
            section.QuerySelectorAll("dl dd").Select(TextOf));
        var row = Assert.Single(section.QuerySelectorAll(".table-hover tbody tr"));
        Assert.Contains("29 Oct 2026", TextOf(row));
        Assert.Contains("1:30 PM", TextOf(row));
        var createdAtText = TextOf(row.QuerySelectorAll("td")[6]);
        Assert.True(DateTime.TryParseExact(
            createdAtText,
            "d MMM yyyy, h:mm tt",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var createdAt), $"Unexpected booking creation timestamp: {createdAtText}");
        Assert.Equal(new DateTime(2026, 9, 30, 12, 30, 0), createdAt);
        Assert.Equal(note, row.QuerySelector(".report-reservation-note")!.TextContent);
        Assert.Empty(row.QuerySelectorAll("script, img"));
        Assert.Contains($"Customer #{TestCustomer}", TextOf(row));
        Assert.DoesNotContain($"Customer #{OtherCustomer}", TextOf(row));
    }

    [Fact]
    public async Task ReservationRecordLimitPreservesAllTrendAndPeakTotals()
    {
        var bookings = Enumerable.Range(0, 101).Select(index =>
        {
            var booking = Booking(ReservationTimeSlots.All[index % ReservationTimeSlots.All.Length]);
            booking.ReservationDate = new DateOnly(2026, 10, 1).AddDays(index / ReservationTimeSlots.All.Length);
            return booking;
        }).ToArray();
        await SaveBookingsAsync(bookings);

        var page = await GetPageAsync("/Reports?Period=Monthly&ReferenceDate=2026-10-29");
        var section = page.QuerySelector("#reservations")!;
        Assert.Equal(100, section.QuerySelectorAll(".table-hover tbody tr").Length);
        Assert.Contains("Showing the first 100 of 101 reservations.", TextOf(section));
        Assert.Equal(
            new[] { "101", "202", "101", "0", "0.0%" },
            section.QuerySelectorAll("dl dd").Select(TextOf));
        using var json = JsonDocument.Parse(page.QuerySelector("#report-data")!.TextContent);
        var data = json.RootElement.GetProperty("reservations");
        var trend = data.GetProperty("trend");
        Assert.Equal(31, trend.GetProperty("labels").GetArrayLength());
        Assert.Equal(101, trend.GetProperty("scheduled").EnumerateArray().Sum(v => v.GetInt32()));
        Assert.Equal(101, data.GetProperty("status").GetProperty("counts").EnumerateArray().Sum(v => v.GetInt32()));
        Assert.Equal(101, data.GetProperty("peak").GetProperty("counts").EnumerateArray().Sum(v => v.GetInt32()));
    }
}
