using ClosedXML.Excel;
using RROMS.Web.Models.Reports;

namespace RROMS.Web.Services.Reports;

public partial class ReportExportService
{
    /// <summary>Formats the complete report; no database access or metric calculations.</summary>
    public byte[] ExportExcel(DashboardReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        using var workbook = new XLWorkbook();
        workbook.Properties.Title = "RROMS Analytics Report";
        workbook.Properties.Subject = report.PeriodLabel;
        workbook.Properties.Author = "RROMS";

        foreach (var table in BuildTables(report))
        {
            const int headerRow = 8;
            const int maximumRows = 1_048_576;
            if (table.Rows.Count > maximumRows - headerRow - report.Notes.Count - 2)
                throw new InvalidOperationException(
                    $"The {table.Name} report exceeds Excel's worksheet row limit. Choose a shorter reporting period.");

            var sheet = workbook.Worksheets.Add(table.Name);
            sheet.Style.Font.FontName = "Calibri";
            sheet.Style.Font.FontSize = 11;
            sheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

            for (var column = 0; column < table.Columns.Length; column++)
                sheet.Column(column + 1).Width = Math.Clamp(table.Columns[column].Width * 18, 14, 48);

            WriteExcelMetadata(sheet, 1, table.Columns.Length, "RROMS Analytics Report", 26);
            sheet.Cell(1, 1).Style.Font.FontSize = 18;
            sheet.Cell(1, 1).Style.Font.Bold = true;
            WriteExcelMetadata(sheet, 2, table.Columns.Length, $"{table.Name} | {report.PeriodLabel}", 22);
            sheet.Cell(2, 1).Style.Font.Bold = true;
            WriteExcelMetadata(sheet, 3, table.Columns.Length,
                $"Period: {report.From:yyyy-MM-dd} to {report.To:yyyy-MM-dd} (inclusive)", 22);
            WriteExcelMetadata(sheet, 4, table.Columns.Length,
                $"Currency: {DashboardReport.Currency} | Time zone: Asia/Kuala_Lumpur (UTC+8)", 22);
            WriteExcelMetadata(sheet, 5, table.Columns.Length,
                $"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} (Malaysia time)", 22);

            var estimatedLineWidth = Math.Max(20,
                Enumerable.Range(1, table.Columns.Length).Sum(i => sheet.Column(i).Width));
            var basisHeight = Math.Max(30, Math.Ceiling(table.Basis.Length / estimatedLineWidth) * 16 + 12);
            WriteExcelMetadata(sheet, 6, table.Columns.Length, table.Basis, Math.Min(409, basisHeight));
            sheet.Cell(6, 1).Style.Font.FontColor = XLColor.DarkSlateGray;

            for (var column = 0; column < table.Columns.Length; column++)
                WriteExcelText(sheet.Cell(headerRow, column + 1), table.Columns[column].Title);

            var headings = sheet.Range(headerRow, 1, headerRow, table.Columns.Length);
            headings.Style.Font.Bold = true;
            headings.Style.Font.FontColor = XLColor.White;
            headings.Style.Fill.BackgroundColor = XLColor.FromHtml("#0E5A5A");
            headings.Style.Alignment.WrapText = true;
            sheet.Row(headerRow).Height = 34;

            var rowNumber = headerRow + 1;
            foreach (var row in table.Rows)
            {
                for (var column = 0; column < table.Columns.Length; column++)
                {
                    var cell = sheet.Cell(rowNumber, column + 1);
                    WriteExcelValue(cell, row[column]);
                    if (CellNumberFormat(table, row, column) is { Length: > 0 } format)
                        cell.Style.NumberFormat.Format = format;
                }
                rowNumber++;
            }

            if (table.Rows.Count > 0)
            {
                var dataRange = sheet.Range(headerRow + 1, 1, rowNumber - 1, table.Columns.Length);
                dataRange.Style.Alignment.WrapText = true;
                sheet.Range(headerRow, 1, rowNumber - 1, table.Columns.Length).SetAutoFilter();
            }
            else
            {
                WriteExcelText(sheet.Cell(rowNumber++, 1), "No records for this period.");
                sheet.Range(headerRow, 1, headerRow, table.Columns.Length).SetAutoFilter();
            }

            sheet.SheetView.FreezeRows(headerRow);
            rowNumber++;
            foreach (var note in report.Notes)
            {
                var noteHeight = Math.Max(30, Math.Ceiling(note.Length / estimatedLineWidth) * 16 + 12);
                WriteExcelMetadata(sheet, rowNumber++, table.Columns.Length, note, Math.Min(409, noteHeight));
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteExcelMetadata(
        IXLWorksheet sheet, int row, int columns, string text, double height)
    {
        var range = sheet.Range(row, 1, row, columns);
        if (columns > 1)
            range.Merge();
        WriteExcelText(sheet.Cell(row, 1), text);
        range.Style.Alignment.WrapText = true;
        sheet.Row(row).Height = height;
    }

    private static void WriteExcelValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                WriteExcelText(cell, "N/A");
                break;
            case string text:
                WriteExcelText(cell, text);
                break;
            case int number:
                cell.Value = number;
                cell.Style.NumberFormat.Format = "#,##0";
                break;
            case long number:
                cell.Value = number;
                cell.Style.NumberFormat.Format = "#,##0";
                break;
            case decimal number:
                cell.Value = number;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case double number:
                cell.Value = number;
                break;
            case bool boolean:
                cell.Value = boolean;
                break;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.NumberFormat.Format = "yyyy-mm-dd";
                break;
            case TimeOnly time:
                cell.Value = time.ToTimeSpan();
                cell.Style.NumberFormat.Format = "hh:mm";
                break;
            case DateTime timestamp:
                cell.Value = timestamp;
                cell.Style.NumberFormat.Format = "yyyy-mm-dd hh:mm:ss";
                break;
            default:
                WriteExcelText(cell, FormatValue(value));
                break;
        }
    }

    private static void WriteExcelText(IXLCell cell, string text)
    {
        if (text.Length > 32_767)
            throw new InvalidOperationException(
                "A report text value exceeds Excel's 32,767-character cell limit. Use CSV to preserve the full text.");

        // ClosedXML 0.105 assigns string values as text, never FormulaA1.
        // Its Value setter treats a leading apostrophe as a quote-prefix marker;
        // rich text preserves that original character, including at the cell limit.
        if (text.StartsWith('\''))
            cell.CreateRichText().AddText(text);
        else
            cell.Value = text;

        cell.Style.IncludeQuotePrefix = true;
    }
}
