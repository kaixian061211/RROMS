using System.Text;
using ClosedXML.Excel;
using Microsoft.VisualBasic.FileIO;
using QuestPDF.Infrastructure;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Reports;
using RROMS.Web.Services.Reports;

namespace RROMS.Tests.Reports;

public class ReportExportServiceTests
{
    private readonly ReportExportService _exports = new();

    [Fact]
    public void ExcelPreservesCompleteRowsAndTypedValuesWithoutFormulas()
    {
        var report = ReportSamples.Create();
        // The formatter must use the supplied total rather than derive one from rows.
        report = report with { Sales = report.Sales with { GrossCollections = 987.65m } };
        using var workbook = new XLWorkbook(new MemoryStream(_exports.ExportExcel(report)));

        Assert.Equal(14, workbook.Worksheets.Count);
        var summary = workbook.Worksheet("Summary");
        var gross = summary.RowsUsed().Single(row => row.Cell(2).GetString() == "Gross collections");
        Assert.Equal(987.65m, gross.Cell(3).GetValue<decimal>());
        Assert.Equal(XLDataType.Number, gross.Cell(3).DataType);
        var cancellation = summary.RowsUsed().Single(row => row.Cell(2).GetString() == "Cancellation percentage");
        Assert.Equal("0.0", cancellation.Cell(3).Style.NumberFormat.Format);

        var menu = workbook.Worksheet("Menu Items");
        Assert.True(menu.AutoFilter.IsEnabled);
        Assert.Equal(113, menu.AutoFilter.Range.RangeAddress.LastAddress.RowNumber);
        Assert.Equal(105, menu.Cell(113, 1).GetValue<int>());
        Assert.Equal("Menu tail 105", menu.Cell(113, 3).GetString());
        Assert.Equal(XLDataType.Number, menu.Cell(9, 6).DataType);
        Assert.Equal(2.5m, menu.Cell(9, 6).GetValue<decimal>());
        Assert.Equal(25m, workbook.Worksheet("Table Utilization").Cell(9, 7).GetValue<decimal>());
        Assert.Equal("0.0\"%\"", workbook.Worksheet("Table Utilization").Cell(9, 7).Style.NumberFormat.Format);

        foreach (var name in new[] { "Orders", "Reservations", "Table Utilization", "Customers" })
            Assert.Equal(113, workbook.Worksheet(name).AutoFilter.Range.RangeAddress.LastAddress.RowNumber);

        foreach (var (text, index) in ReportSamples.Texts.Select((text, index) => (text, index)))
            // Worksheet XML normalizes CRLF to LF while preserving the line break.
            Assert.Equal(text.ReplaceLineEndings("\n"), menu.Cell(index + 9, 3).GetString().ReplaceLineEndings("\n"));
        Assert.All(workbook.Worksheets.SelectMany(sheet => sheet.CellsUsed()), cell => Assert.False(cell.HasFormula));
        Assert.Equal(report.GeneratedAt, workbook.Worksheet("Orders").Cell(9, 2).GetDateTime());
        Assert.Equal(XLDataType.DateTime, workbook.Worksheet("Reservations").Cell(9, 2).DataType);
        Assert.Equal(-25m, workbook.Worksheet("Customers").Cell(9, 8).GetValue<decimal>());
        SaveArtifact("sample-report.xlsx", _exports.ExportExcel(ReportSamples.Create()));
    }

    [Fact]
    public void CsvIsRectangularCompleteAndEscapesUserTextWithoutChangingNegativeNumbers()
    {
        var bytes = _exports.ExportCsv(ReportSamples.Create());
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        var records = ReadCsv(bytes);
        Assert.Equal(new[] { "Section", "Record", "Field", "Value" }, records[0]);
        Assert.All(records, record => Assert.Equal(4, record.Length));
        Assert.Contains(records, row => row.SequenceEqual(new[] { "Menu Items", "105", "Name", "Menu tail 105" }));
        Assert.Contains(records, row => row.SequenceEqual(new[] { "Customers", "1", "Net spending (MYR)", "-25" }));
        Assert.Contains(records, row => row.SequenceEqual(new[] { "Menu Items", "1", "Name", "'=1+1" }));
        Assert.Contains(records, row => row.SequenceEqual(new[] { "Menu Items", "6", "Name", "'quoted" }));
        Assert.Contains(records, row => row.SequenceEqual(new[] { "Menu Items", "7", "Name", ReportSamples.Texts[6] }));
        Assert.Equal(14, records.Count(row => row[2] == "Basis" && row[1] == string.Empty));
        Assert.Contains(records, row => row[0] == "Report" && row[2] == "Time zone");
        SaveArtifact("sample-report.csv", bytes);
    }

    [Fact]
    public void PdfCanPaginateAllSectionsIncludingLongNotes()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var bytes = _exports.ExportPdf(ReportSamples.Create());
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes.Take(8).ToArray()));
        Assert.True(bytes.Length > 10_000);
        SaveArtifact("sample-report.pdf", bytes);
    }

    [Fact]
    public void EmptyResultsProduceAllFormatsWithNotesAndNA()
    {
        var report = ReportSamples.Create(0);
        using var workbook = new XLWorkbook(new MemoryStream(_exports.ExportExcel(report)));
        Assert.Equal(14, workbook.Worksheets.Count);
        Assert.Equal("No records for this period.", workbook.Worksheet("Customers").Cell(9, 1).GetString());
        var summary = workbook.Worksheet("Summary");
        Assert.Equal("N/A", summary.RowsUsed().Single(row => row.Cell(2).GetString() == "Average payment").Cell(3).GetString());
        Assert.Contains(ReadCsv(_exports.ExportCsv(report)), row => row[0] == "Customers" && row[2] == "Record count" && row[3] == "0");
        QuestPDF.Settings.License = LicenseType.Community;
        Assert.NotEmpty(_exports.ExportPdf(report));
    }

    [Theory]
    [InlineData(ReportPeriod.Daily, "2026-10-01")]
    [InlineData(ReportPeriod.Weekly, "2026-10-01")]
    [InlineData(ReportPeriod.Monthly, "2026-10")]
    [InlineData(ReportPeriod.Yearly, "2026")]
    public void FilenamesUseReportPeriodAndStartDate(ReportPeriod period, string date)
    {
        var report = ReportSamples.Create(0) with { Period = period };
        Assert.Equal($"RROMS-Report-{period}-{date}.csv", ReportExportService.FileName(report, "csv"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportExportService.FileName(report, "exe"));
    }

    internal static List<string[]> ReadCsv(byte[] bytes)
    {
        using var parser = new TextFieldParser(new MemoryStream(bytes), Encoding.UTF8, true);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = false;
        var records = new List<string[]>();
        while (!parser.EndOfData)
            records.Add(parser.ReadFields()!);
        return records;
    }

    internal static void SaveArtifact(string name, byte[] bytes)
    {
        if (Environment.GetEnvironmentVariable("RROMS_REPORT_ARTIFACT_DIR") is not { Length: > 0 } path)
            return;
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, name), bytes);
    }
}

internal static class ReportSamples
{
    internal static readonly string[] Texts =
        ["=1+1", "+SUM(A1)", "-text", "@value", " \t=1+1", "'quoted", "Crème, \"quoted\"\r\nline", "中文菜品"];

    internal static DashboardReport Create(int count = 105)
    {
        var at = new DateTime(2026, 10, 29, 13, 30, 0);
        var ids = Enumerable.Range(1, count).ToArray();
        var note = string.Join(" ", Enumerable.Repeat("Long reservation note.", 350)) + " NOTE_END";
        var transactions = ids.Select(i => new SalesTransaction(at, SalesTransactionType.Payment,
            i, i, i, PaymentMethod.Cash, 2.5m, $"Reference {i}", null)).ToList();
        if (count > 0)
            transactions.Add(new SalesTransaction(at, SalesTransactionType.Refund, 999, 999, null, PaymentMethod.Card, -25m, "Refund", "Refund note"));

        return new DashboardReport(ReportPeriod.Monthly, "October 2026", new(2026, 10, 1), new(2026, 10, 31), at,
            new SalesReport(count * 2.5m, count, count > 0 ? 25m : 0, count > 0 ? 1 : 0,
                count * 2.5m - (count > 0 ? 25m : 0), count > 0 ? 2.5m : null,
                [new("29 Oct 2026", count * 2.5m, count, count > 0 ? 25m : 0, count * 2.5m - (count > 0 ? 25m : 0))],
                [new(PaymentMethod.Cash, count, count * 2.5m, 0, count * 2.5m, count > 0 ? 100m : null)],
                transactions, "Collections use payment dates; refunds use their own dates."),
            new OrderReport(count, 0, count * 2.5m, count > 0 ? 2.5m : null,
                [new(OrderStatus.Completed, count)], [new("29 Oct 2026", count, count * 2.5m)],
                ids.Select(i => new OrderRow(i, at, i, $"T{i}", OrderStatus.Completed, 1, 2.5m, PaymentStatus.Paid)).ToList(),
                "Orders use creation dates and saved prices."),
            new ReservationReport(count, count * 2, [new(ReservationStatus.Confirmed, count)], count, 0, count > 0 ? 0m : null,
                [new("29 Oct 2026", count, 0)], [new(new(13, 30), count, count * 2, count > 0)],
                ids.Select(i => new ReservationRow(i, new(2026, 10, 29), new(13, 30), $"T{i}", "Main hall", i, 2,
                    ReservationStatus.Confirmed, at, i == 1 ? note : $"Reservation tail {i}")).ToList(),
                "Scheduled dates are local; cancellation reflects current status."),
            new TableUtilizationReport(count * 4, count, count > 0 ? 25m : null, 0, 0,
                ids.Select(i => new TableUtilizationRow(i, $"T{i}", "Main hall", 4, 4, 1, 25m)).ToList(),
                "Estimated booked-slot utilization using current active tables and opening hours."),
            new PopularMenuReport(count, count * 2.5m,
                ids.Select(i => new PopularMenuItemSummary(i, i, i <= Texts.Length ? Texts[i - 1] : $"Menu tail {i}",
                    "Mains", 1, 2.5m, count > 0 ? Math.Round(100m / count, 1) : null)).ToList(),
                "Currently paid purchases only; fully refunded purchases are excluded."),
            new CustomerActivityReport(count, 0,
                ids.Select(i => new CustomerActivitySummary(i, $"Customer #{i}", 1, 1, i == 1 ? 0 : 1,
                    i == 1 ? 0 : 2.5m, i == 1 ? 25 : 0, i == 1 ? -25 : 2.5m, 1, false)).ToList(),
                "Returning customers are an estimate based on distinct order dates."),
            ["Amounts are MYR. All timestamps are already Malaysia-local.", "Reports can change when regenerated."]);
    }
}
