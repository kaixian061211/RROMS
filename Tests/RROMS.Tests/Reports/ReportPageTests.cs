using System.Net;
using System.Text.Json;
using AngleSharp.Dom;
using RROMS.Tests.Integration;
using RROMS.Web.Models.Dev;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Reports;

namespace RROMS.Tests.Reports;

[Trait("Type", "Integration")]
public class ReportPageTests : PageTest
{
    public ReportPageTests(TestWebApp app) : base(app) { }

    protected override TestUser? SignedInAs => TestUsers.Admin;

    [Theory]
    [InlineData("Daily", "Thursday, 29 October 2026")]
    [InlineData("Weekly", "26 Oct 2026 to 1 Nov 2026")]
    [InlineData("Monthly", "October 2026")]
    [InlineData("Yearly", "2026")]
    public async Task AdminCanSelectEachPeriod(string period, string label)
    {
        var page = await GetPageAsync($"/Reports?Period={period}&ReferenceDate=2026-10-29");

        Assert.Equal(label, TextOf(page.QuerySelector(".report-meta dd")!));
        Assert.Contains("No data for this period.", TextOf(page));
        Assert.Equal(6, page.QuerySelectorAll(".report-summary article").Length);
        Assert.NotNull(page.QuerySelector("a[href='/Reports'].active"));
        Assert.NotNull(page.QuerySelector("a[href='/Admin/Accounts']"));
        var date = page.QuerySelector("input[name='ReferenceDate']")!;
        Assert.Equal("2026-10-29", date.GetAttribute("value"));
        Assert.Equal("2000-01-01", date.GetAttribute("min"));
        Assert.Equal("2100-12-31", date.GetAttribute("max"));
    }

    [Theory]
    [InlineData("Period=Unknown&ReferenceDate=2026-10-29")]
    [InlineData("Period=99&ReferenceDate=2026-10-29")]
    [InlineData("Period=Daily&ReferenceDate=invalid")]
    [InlineData("Period=Daily&ReferenceDate=1999-12-31")]
    [InlineData("Period=Daily&ReferenceDate=2101-01-01")]
    public async Task InvalidFiltersShowValidationWithoutReport(string query)
    {
        var page = await GetPageAsync($"/Reports?{query}");

        Assert.NotNull(page.QuerySelector(".validation-summary-errors"));
        Assert.Null(page.QuerySelector(".report-summary"));
        Assert.Null(page.QuerySelector("#report-data"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task CustomersAndStaffCannotAccessReports(int userId)
    {
        await SignInAsAsync(TestUsers.Find(userId)!);
        var response = await Client.GetAsync("/Reports");

        Assert.Equal("/Account/AccessDenied", response.RequestMessage!.RequestUri!.AbsolutePath);
        var home = await GetPageAsync("/");
        Assert.Null(home.QuerySelector("a[href='/Reports']"));
    }

    [Fact]
    public async Task VisitorsMustLogIn()
    {
        await SignOutAsync();
        var response = await Client.GetAsync("/Reports");

        Assert.Equal("/Account/Login", response.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ReportDisplaysServiceFiguresAndDisablesCaching()
    {
        await WithDatabaseAsync(async db =>
        {
            db.Payments.Add(new Payment
            {
                OrderId = 987654,
                Amount = 123.45m,
                PaymentMethod = PaymentMethod.Cash,
                PaymentStatus = PaymentStatus.Paid,
                CreatedAt = ReportDateRange.ToStored(new DateTime(2026, 10, 29, 12, 0, 0)),
                PaidAt = ReportDateRange.ToStored(new DateTime(2026, 10, 29, 12, 0, 0))
            });
            await db.SaveChangesAsync();
        });

        var response = await Client.GetAsync("/Reports?Period=Daily&ReferenceDate=2026-10-29");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.True(response.Headers.CacheControl?.NoCache == true);
        var page = await GetPageAsync("/Reports?Period=Daily&ReferenceDate=2026-10-29");
        Assert.Contains("RM 123.45", TextOf(page.QuerySelector(".report-summary")!));
        Assert.DoesNotContain("No data for this period.", TextOf(page));
        Assert.NotNull(page.QuerySelector("#sales-trend-chart"));
        Assert.NotNull(page.QuerySelector("#sales-method-chart"));
        Assert.Contains("RM 123.45", TextOf(page.QuerySelector("#sales")!));
        using var json = JsonDocument.Parse(page.QuerySelector("#report-data")!.TextContent);
        var sales = json.RootElement.GetProperty("sales");
        Assert.Equal(123.45m, sales.GetProperty("trend").GetProperty("collections")[12].GetDecimal());
        Assert.Equal(123.45m, sales.GetProperty("methods").GetProperty("amounts")[0].GetDecimal());
        Assert.Equal("Cash", sales.GetProperty("methods").GetProperty("labels")[0].GetString());
    }

    [Fact]
    public async Task RefundOnlyPeriodShowsNegativeNetWithoutMethodChart()
    {
        const string note = "</td><script>alert('test')</script>";
        await WithDatabaseAsync(async db =>
        {
            db.Payments.Add(new Payment
            {
                OrderId = 987655,
                Amount = 25m,
                PaymentMethod = PaymentMethod.Card,
                PaymentStatus = PaymentStatus.Refunded,
                CreatedAt = ReportDateRange.ToStored(new DateTime(2026, 9, 30, 12, 0, 0)),
                PaidAt = ReportDateRange.ToStored(new DateTime(2026, 9, 30, 12, 0, 0)),
                RefundedAt = ReportDateRange.ToStored(new DateTime(2026, 10, 29, 12, 0, 0)),
                RefundReason = note
            });
            await db.SaveChangesAsync();
        });

        var page = await GetPageAsync("/Reports?Period=Daily&ReferenceDate=2026-10-29");
        var section = page.QuerySelector("#sales")!;
        Assert.NotNull(section.QuerySelector("#sales-trend-chart"));
        Assert.Null(section.QuerySelector("#sales-method-chart"));
        Assert.Contains("No collections for this period.", TextOf(section));
        Assert.Contains("RM -25.00", TextOf(section));
        Assert.Contains(note, section.QuerySelector(".report-ledger-note")!.TextContent);
        Assert.Empty(section.QuerySelectorAll("script"));
        using var json = JsonDocument.Parse(page.QuerySelector("#report-data")!.TextContent);
        var trend = json.RootElement.GetProperty("sales").GetProperty("trend");
        Assert.Equal(-25m, trend.GetProperty("net")[12].GetDecimal());
        Assert.Equal(25m, trend.GetProperty("refunds")[12].GetDecimal());
    }

    [Fact]
    public async Task LedgerLimitDoesNotTruncateChartTotals()
    {
        await WithDatabaseAsync(async db =>
        {
            var paidAt = ReportDateRange.ToStored(new DateTime(2026, 10, 29, 12, 0, 0));
            for (var i = 0; i < 101; i++)
            {
                db.Payments.Add(new Payment
                {
                    OrderId = 700000 + i,
                    Amount = 1m,
                    PaymentMethod = PaymentMethod.Cash,
                    PaymentStatus = PaymentStatus.Paid,
                    CreatedAt = paidAt,
                    PaidAt = paidAt
                });
            }
            await db.SaveChangesAsync();
        });

        var page = await GetPageAsync("/Reports?Period=Daily&ReferenceDate=2026-10-29");
        var ledger = page.QuerySelector("#sales .table-hover")!;
        Assert.Equal(100, ledger.QuerySelectorAll("tbody tr").Length);
        Assert.Contains("Showing the first 100 of 101 transactions.", TextOf(page));
        using var json = JsonDocument.Parse(page.QuerySelector("#report-data")!.TextContent);
        var sales = json.RootElement.GetProperty("sales");
        Assert.Equal(101m, sales.GetProperty("trend").GetProperty("collections")[12].GetDecimal());
        Assert.Equal(101m, sales.GetProperty("methods").GetProperty("amounts")[0].GetDecimal());
    }

    [Fact]
    public async Task EmptySalesKeepTrendTableWithoutChartCanvases()
    {
        var page = await GetPageAsync("/Reports?Period=Daily&ReferenceDate=2026-10-29");
        var section = page.QuerySelector("#sales")!;
        Assert.Empty(section.QuerySelectorAll("canvas"));
        Assert.Contains("No transactions for this period.", TextOf(section));
        Assert.Equal(24, section.QuerySelectorAll("details tbody tr").Length);
        using var json = JsonDocument.Parse(page.QuerySelector("#report-data")!.TextContent);
        Assert.Equal(24, json.RootElement.GetProperty("sales").GetProperty("trend").GetProperty("labels").GetArrayLength());
        var orders = page.QuerySelector("#orders")!;
        Assert.Empty(orders.QuerySelectorAll("canvas"));
        Assert.Contains("No orders for this period.", TextOf(orders));
        Assert.Equal(24, orders.QuerySelectorAll("details tbody tr").Length);
    }

    [Fact]
    public async Task OrdersChartsAndRecordsReconcileWithoutCancelledValue()
    {
        await WithDatabaseAsync(async db =>
        {
            foreach (var (status, quantity, price) in new[]
            {
                (OrderStatus.Completed, 2, 12m),
                (OrderStatus.Pending, 1, 6m),
                (OrderStatus.Cancelled, 1, 9m)
            })
            {
                db.Add(new Order
                {
                    CustomerUserId = TestCustomer,
                    TableId = 1,
                    Status = status,
                    CreatedAt = ReportDateRange.ToStored(new DateTime(2026, 10, 29, 12, 30, 0)),
                    Items = new List<OrderItem>
                    {
                        new() { MenuItemId = NasiLemak, Quantity = quantity, UnitPrice = price }
                    }
                });
            }
            await db.SaveChangesAsync();
        });

        var page = await GetPageAsync("/Reports?Period=Daily&ReferenceDate=2026-10-29");
        var orders = page.QuerySelector("#orders")!;
        Assert.NotNull(orders.QuerySelector("#orders-status-chart"));
        Assert.NotNull(orders.QuerySelector("#orders-trend-chart"));
        var summary = orders.QuerySelectorAll("dl dd").Select(TextOf).ToArray();
        Assert.Equal(new[] { "3", "1", "RM 30.00", "RM 15.00" }, summary);
        var rows = orders.QuerySelectorAll(".table-hover tbody tr");
        Assert.Equal(3, rows.Length);
        Assert.Contains(rows, row => TextOf(row).Contains("Cancelled") && TextOf(row).Contains("RM 9.00"));
        Assert.All(rows, row => Assert.Contains("29 Oct 2026, 12:30 PM", TextOf(row)));
        Assert.All(rows, row => Assert.Contains("Not recorded", TextOf(row)));

        using var json = JsonDocument.Parse(page.QuerySelector("#report-data")!.TextContent);
        var chart = json.RootElement.GetProperty("orders");
        Assert.Equal(3, chart.GetProperty("status").GetProperty("counts").EnumerateArray().Sum(v => v.GetInt32()));
        Assert.Equal(3, chart.GetProperty("trend").GetProperty("counts")[12].GetInt32());
        Assert.Equal(30m, chart.GetProperty("trend").GetProperty("values")[12].GetDecimal());
        Assert.NotNull(page.QuerySelector("#sales"));
        Assert.Equal(0m, json.RootElement.GetProperty("sales").GetProperty("trend").GetProperty("collections")[12].GetDecimal());
    }

    [Fact]
    public async Task OrderRecordLimitPreservesFullAggregates()
    {
        await WithDatabaseAsync(async db =>
        {
            var placedAt = ReportDateRange.ToStored(new DateTime(2026, 10, 29, 12, 0, 0));
            for (var i = 0; i < 101; i++)
            {
                db.Add(new Order
                {
                    CustomerUserId = TestCustomer,
                    TableId = 1,
                    Status = OrderStatus.Pending,
                    CreatedAt = placedAt,
                    Items = new List<OrderItem>
                    {
                        new() { MenuItemId = NasiLemak, Quantity = 1, UnitPrice = 1m }
                    }
                });
            }
            await db.SaveChangesAsync();
        });

        var page = await GetPageAsync("/Reports?Period=Daily&ReferenceDate=2026-10-29");
        var orders = page.QuerySelector("#orders")!;
        Assert.Equal(100, orders.QuerySelectorAll(".table-hover tbody tr").Length);
        Assert.Contains("Showing the first 100 of 101 orders.", TextOf(orders));
        using var json = JsonDocument.Parse(page.QuerySelector("#report-data")!.TextContent);
        var trend = json.RootElement.GetProperty("orders").GetProperty("trend");
        Assert.Equal(101, trend.GetProperty("counts")[12].GetInt32());
        Assert.Equal(101m, trend.GetProperty("values")[12].GetDecimal());
    }
}
