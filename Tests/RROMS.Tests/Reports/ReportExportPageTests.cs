using System.Net;
using System.Text;
using System.Text.Json;
using AngleSharp.Dom;
using ClosedXML.Excel;
using RROMS.Tests.Integration;
using RROMS.Web.Models.Dev;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.Reports;

namespace RROMS.Tests.Reports;

[Trait("Type", "Integration")]
public class ReportExportPageTests : PageTest
{
    public ReportExportPageTests(TestWebApp app) : base(app) { }
    protected override TestUser? SignedInAs => TestUsers.Admin;

    public static IEnumerable<object[]> DownloadCases =>
        from period in new[] { "Daily", "Weekly", "Monthly", "Yearly" }
        from format in new[] { "Pdf", "Excel", "Csv" }
        select new object[] { period, format };

    [Theory]
    [MemberData(nameof(DownloadCases))]
    public async Task AdminCanDownloadEveryFormatForEveryPeriod(string period, string format)
    {
        var response = await Client.GetAsync($"/Reports?handler=Export{format}&Period={period}&ReferenceDate=2026-10-29");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var date = period switch { "Monthly" => "2026-10", "Yearly" => "2026", "Weekly" => "2026-10-26", _ => "2026-10-29" };
        var extension = format switch { "Excel" => "xlsx", "Pdf" => "pdf", _ => "csv" };
        var disposition = response.Content.Headers.ContentDisposition!;
        Assert.Equal($"RROMS-Report-{period}-{date}.{extension}", (disposition.FileNameStar ?? disposition.FileName)!.Trim('"'));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        switch (format)
        {
            case "Pdf":
                Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
                Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes.Take(8).ToArray()));
                break;
            case "Excel":
                Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
                using (var workbook = new XLWorkbook(new MemoryStream(bytes)))
                    Assert.Equal(14, workbook.Worksheets.Count);
                break;
            default:
                Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
                Assert.Contains(ReportExportServiceTests.ReadCsv(bytes), row => row[0] == "Report" && row[2] == "Currency" && row[3] == "MYR");
                break;
        }
    }

    [Theory]
    [InlineData("Pdf")]
    [InlineData("Excel")]
    [InlineData("Csv")]
    public async Task InvalidExportFiltersReturn400(string format)
    {
        foreach (var query in new[]
        {
            "Period=Unknown&ReferenceDate=2026-10-29", "Period=99&ReferenceDate=2026-10-29",
            "Period=Daily&ReferenceDate=invalid", "Period=Daily&ReferenceDate=1999-12-31",
            "Period=Daily&ReferenceDate=2101-01-01"
        })
        {
            var response = await Client.GetAsync($"/Reports?handler=Export{format}&{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Null(response.Content.Headers.ContentDisposition);
        }
    }

    [Theory]
    [InlineData("Pdf")]
    [InlineData("Excel")]
    [InlineData("Csv")]
    public async Task ExportHandlersRequireAnAdmin(string format)
    {
        foreach (var user in new[] { TestUsers.CustomerA, TestUsers.Staff })
        {
            await SignInAsAsync(user);
            var denied = await Client.GetAsync($"/Reports?handler=Export{format}");
            Assert.Equal("/Account/AccessDenied", denied.RequestMessage!.RequestUri!.AbsolutePath);
            Assert.Null(denied.Content.Headers.ContentDisposition);
        }

        await SignOutAsync();
        var visitor = await Client.GetAsync($"/Reports?handler=Export{format}");
        Assert.Equal("/Account/Login", visitor.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task DashboardAndExportsAgreeForCustomerRefundsAndPaidOnlyMenu()
    {
        await WithDatabaseAsync(async db =>
        {
            var first = ReportDateRange.ToStored(new DateTime(2026, 10, 29, 12, 0, 0));
            var second = ReportDateRange.ToStored(new DateTime(2026, 10, 30, 13, 30, 0));
            var paid = new Order { CustomerUserId = TestCustomer, TableId = 1, Status = OrderStatus.Completed,
                CreatedAt = first, Items = [new() { MenuItemId = TehTarik, Quantity = 1, UnitPrice = 12m }] };
            var refunded = new Order { CustomerUserId = TestCustomer, TableId = 1, Status = OrderStatus.Completed,
                CreatedAt = second, Items = [new() { MenuItemId = NasiLemak, Quantity = 1, UnitPrice = 25m }] };
            db.AddRange(paid, refunded);
            await db.SaveChangesAsync();
            db.Payments.AddRange(
                new Payment { OrderId = paid.OrderId, Amount = 12m, PaymentMethod = PaymentMethod.Cash,
                    PaymentStatus = PaymentStatus.Paid, CreatedAt = first, PaidAt = first },
                new Payment { OrderId = refunded.OrderId, Amount = 25m, PaymentMethod = PaymentMethod.Card,
                    PaymentStatus = PaymentStatus.Refunded, CreatedAt = second, PaidAt = second, RefundedAt = second });
            db.Add(new Reservation { CustomerUserId = TestCustomer, TableId = 1, ReservationDate = new(2026, 10, 31),
                TimeSlot = new(13, 30), PartySize = 2, Status = ReservationStatus.Confirmed, CreatedAt = first });
            await db.SaveChangesAsync();
        });

        const string query = "Period=Monthly&ReferenceDate=2026-10-29";
        var page = await GetPageAsync($"/Reports?{query}");
        foreach (var section in new[] { "sales", "orders", "reservations", "utilization", "popular-menu", "customers" })
            Assert.NotNull(page.QuerySelector($"#{section}"));
        var customers = page.QuerySelector("#customers")!;
        Assert.Equal(new[] { "1", "1" }, customers.QuerySelectorAll("dl dd").Select(TextOf));
        var cells = customers.QuerySelectorAll(".table-hover tbody tr td");
        Assert.Equal("RM 37.00", TextOf(cells[3]));
        Assert.Equal("RM 25.00", TextOf(cells[4]));
        Assert.Equal("RM 12.00", TextOf(cells[5]));
        Assert.Equal("2", TextOf(cells[6]));
        Assert.Equal("Yes", TextOf(cells[7]));
        using var chart = JsonDocument.Parse(page.QuerySelector("#report-data")!.TextContent);
        Assert.Equal(1, chart.RootElement.GetProperty("menu").GetProperty("labels").GetArrayLength());
        Assert.Equal(1, chart.RootElement.GetProperty("menu").GetProperty("quantities")[0].GetInt32());

        foreach (var format in new[] { "Pdf", "Excel", "Csv" })
        {
            var link = page.QuerySelector($"a[href*='handler=Export{format}']")!;
            Assert.Contains("Period=Monthly", link.GetAttribute("href"));
            Assert.Contains("ReferenceDate=2026-10-29", link.GetAttribute("href"));
        }

        var response = await Client.GetAsync($"/Reports?handler=ExportExcel&{query}");
        using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        Assert.Equal(12m, workbook.Worksheet("Customers").Cell(9, 8).GetValue<decimal>());
        Assert.Equal(12m, workbook.Worksheet("Menu Items").Cell(9, 6).GetValue<decimal>());
        Assert.Equal(9, workbook.Worksheet("Menu Items").AutoFilter.Range.RangeAddress.LastAddress.RowNumber);
        var csvResponse = await Client.GetAsync($"/Reports?handler=ExportCsv&{query}");
        Assert.Contains(ReportExportServiceTests.ReadCsv(await csvResponse.Content.ReadAsByteArrayAsync()),
            row => row.SequenceEqual(new[] { "Customers", "1", "Net spending (MYR)", "12.00" }));

        var html = await Client.GetStringAsync($"/Reports?{query}");
        ReportExportServiceTests.SaveArtifact("dashboard.html", Encoding.UTF8.GetBytes(html));
    }

    [Fact]
    public async Task MenuAndCustomerLimitsDoNotTruncateTotalsOrExports()
    {
        const string hostileName = "</script><script>alert('test')</script>";
        await WithDatabaseAsync(async db =>
        {
            var at = ReportDateRange.ToStored(new DateTime(2026, 10, 29, 12, 0, 0));
            var category = new MenuCategory { Name = "Report checks" };
            var items = Enumerable.Range(0, 105).Select(i => new MenuItem
                { Name = i == 0 ? hostileName : $"Menu {i}", MenuCategory = category, Price = 1m, IsAvailable = true }).ToList();
            db.AddRange(items);
            await db.SaveChangesAsync();
            for (var i = 0; i < 105; i++)
            {
                var order = new Order { CustomerUserId = 10001 + i, TableId = 1, CreatedAt = at,
                    Status = OrderStatus.Completed, Items = [new() { MenuItemId = items[i].MenuItemId, Quantity = 1, UnitPrice = 1m }] };
                db.Add(order);
                await db.SaveChangesAsync();
                db.Payments.Add(new Payment { OrderId = order.OrderId, Amount = 1m, PaymentMethod = PaymentMethod.Cash,
                    PaymentStatus = PaymentStatus.Paid, CreatedAt = at, PaidAt = at });
            }
            await db.SaveChangesAsync();
        });

        var page = await GetPageAsync("/Reports?Period=Daily&ReferenceDate=2026-10-29");
        Assert.Equal(100, page.QuerySelectorAll("#popular-menu .table-hover tbody tr").Length);
        Assert.Equal(100, page.QuerySelectorAll("#customers .table-hover tbody tr").Length);
        Assert.Equal("105", TextOf(page.QuerySelector("#customers dl dd")!));
        Assert.Empty(page.QuerySelectorAll("#popular-menu script"));
        Assert.Contains(hostileName, page.QuerySelector("#popular-menu .report-menu-name")!.TextContent);
        using var json = JsonDocument.Parse(page.QuerySelector("#report-data")!.TextContent);
        Assert.Equal(10, json.RootElement.GetProperty("menu").GetProperty("labels").GetArrayLength());
        Assert.Equal(hostileName, json.RootElement.GetProperty("menu").GetProperty("labels")[0].GetString());
        var download = await Client.GetByteArrayAsync("/Reports?handler=ExportExcel&Period=Daily&ReferenceDate=2026-10-29");
        using var workbook = new XLWorkbook(new MemoryStream(download));
        Assert.Equal(113, workbook.Worksheet("Menu Items").AutoFilter.Range.RangeAddress.LastAddress.RowNumber);
        Assert.Equal(113, workbook.Worksheet("Customers").AutoFilter.Range.RangeAddress.LastAddress.RowNumber);
        ReportExportServiceTests.SaveArtifact("dashboard-large.html",
            Encoding.UTF8.GetBytes(await Client.GetStringAsync("/Reports?Period=Daily&ReferenceDate=2026-10-29")));
    }
}
