using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RROMS.Web.Models.Reports;

namespace RROMS.Web.Services.Reports;

public partial class ReportExportService
{
    /// <summary>
    /// Formats the supplied report. Every section and every row is included;
    /// report dates and timestamps are already in Malaysia time.
    /// </summary>
    public byte[] ExportPdf(DashboardReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sections = BuildTables(report);

        return Document.Create(document =>
        {
            foreach (var section in sections)
            {
                document.Page(page =>
                {
                    // Wide record listings remain readable without shrinking
                    // customer listings into a portrait page.
                    page.Size(section.Columns.Length > 8
                        ? PageSizes.A3.Landscape()
                        : PageSizes.A4.Landscape());
                    page.Margin(28);
                    page.DefaultTextStyle(style => style.FontSize(9));

                    page.Header().PaddingBottom(12).Column(header =>
                    {
                        header.Spacing(3);
                        header.Item().Text("RROMS Analytics Report")
                            .FontSize(18).Bold().FontColor("#0E5A5A");
                        header.Item().Text(section.Name).FontSize(13).SemiBold();
                        header.Item().Text(
                            $"{report.PeriodLabel} | Currency: {DashboardReport.Currency}");
                        header.Item().Text($"{report.From:yyyy-MM-dd} to {report.To:yyyy-MM-dd} (inclusive)");
                        header.Item().Text(
                            $"Generated: {FormatValue(report.GeneratedAt)} | Malaysia (UTC+8)")
                            .FontSize(8).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Column(content =>
                    {
                        content.Spacing(10);
                        if (!string.IsNullOrWhiteSpace(section.Basis))
                        {
                            content.Item().Text(section.Basis)
                                .FontSize(9).FontColor(Colors.Grey.Darken2);
                        }

                        if (section.Rows.Count == 0)
                        {
                            content.Item().Text("No records for this period.")
                                .Italic().FontColor(Colors.Grey.Darken1);
                        }

                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                foreach (var column in section.Columns)
                                    columns.RelativeColumn(column.Width);
                            });

                            // QuestPDF repeats this header whenever the table
                            // continues onto another page.
                            table.Header(header =>
                            {
                                foreach (var column in section.Columns)
                                {
                                    header.Cell().Background("#0E5A5A")
                                        .Padding(6).Text(column.Title)
                                        .Bold().FontColor(Colors.White);
                                }
                            });

                            for (var rowIndex = 0; rowIndex < section.Rows.Count; rowIndex++)
                            {
                                var row = section.Rows[rowIndex];
                                for (var columnIndex = 0; columnIndex < section.Columns.Length; columnIndex++)
                                {
                                    var value = row[columnIndex];
                                    var cell = table.Cell()
                                        .Background(rowIndex % 2 == 0 ? "#F2F6F6" : "#FFFFFF")
                                        .BorderBottom(0.5f).BorderColor("#DCE3E3")
                                        .Padding(6);

                                    if (value is int or long or decimal or double or float)
                                        cell = cell.AlignRight();

                                    // Text cells can flow across page boundaries;
                                    // fixed heights and ShowEntire would reject long notes.
                                    cell.Text(PdfValue(value, CellNumberFormat(section, row, columnIndex)));
                                }
                            }
                        });
                    });

                    page.Footer().PaddingTop(10).AlignCenter().Text(text =>
                    {
                        text.Span("RROMS | ").FontColor(Colors.Grey.Darken2);
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            }
        }).GeneratePdf();
    }

    private static string PdfValue(object? value, string? numberFormat) => value switch
    {
        decimal amount => amount.ToString(
            numberFormat ?? "N2", CultureInfo.InvariantCulture),
        _ => FormatValue(value)
    };
}
