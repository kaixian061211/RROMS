using System.Globalization;
using System.Text;
using RROMS.Web.Models.Reports;

namespace RROMS.Web.Services.Reports;

public partial class ReportExportService
{
    /// <summary>
    /// A rectangular CSV covering tables with different columns: Section, Record,
    /// Field, Value. Record numbers are one-based within a section. Metadata uses
    /// an empty Record. The export includes every result row, not the UI limit.
    /// </summary>
    public byte[] ExportCsv(DashboardReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var csv = new StringBuilder();
        csv.Append('\uFEFF'); // UTF-8 BOM lets Excel recognize international text.
        AppendCsvRecord(csv, "Section", "Record", "Field", "Value");

        CsvMetadata(csv, "Report", "Title", "RROMS Analytics Report");
        CsvMetadata(csv, "Report", "Period", report.PeriodLabel);
        CsvMetadata(csv, "Report", "Period start", report.From);
        CsvMetadata(csv, "Report", "Period end (inclusive)", report.To);
        CsvMetadata(csv, "Report", "Generated at", report.GeneratedAt);
        CsvMetadata(csv, "Report", "Time zone", "Asia/Kuala_Lumpur (UTC+8)");
        CsvMetadata(csv, "Report", "Currency", DashboardReport.Currency);
        CsvMetadata(csv, "Report", "Text handling",
            "Potential spreadsheet formulas in text are prefixed with an apostrophe. Numeric values retain their sign.");

        for (var i = 0; i < report.Notes.Count; i++)
            CsvMetadata(csv, "Report", $"Note {i + 1}", report.Notes[i]);

        foreach (var table in BuildTables(report))
        {
            CsvMetadata(csv, table.Name, "Basis", table.Basis);
            CsvMetadata(csv, table.Name, "Record count", table.Rows.Count);
            for (var column = 0; column < table.Columns.Length; column++)
                CsvMetadata(csv, table.Name, $"Column {column + 1}", table.Columns[column].Title);

            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                for (var column = 0; column < table.Columns.Length; column++)
                {
                    AppendCsvRecord(csv,
                        CsvSafeText(table.Name),
                        (rowIndex + 1).ToString(CultureInfo.InvariantCulture),
                        CsvSafeText(table.Columns[column].Title),
                        CsvValue(row[column]));
                }
            }
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private static void CsvMetadata(StringBuilder csv, string section, string field, object? value) =>
        AppendCsvRecord(csv, CsvSafeText(section), string.Empty, CsvSafeText(field), CsvValue(value));

    private static string CsvValue(object? value) =>
        value is string text ? CsvSafeText(text) : FormatValue(value);

    private static string CsvSafeText(string text)
    {
        // Quoting alone does not prevent spreadsheet formula interpretation.
        // Inspect beyond leading spaces/control characters without removing them.
        var first = 0;
        while (first < text.Length &&
               (char.IsWhiteSpace(text[first]) || char.IsControl(text[first])))
            first++;

        var startsWithControl = text.Length > 0 && text[0] is '\t' or '\r' or '\n';
        var startsWithFormula = first < text.Length && text[first] is '=' or '+' or '-' or '@';
        return startsWithControl || startsWithFormula ? "'" + text : text;
    }

    private static void AppendCsvRecord(StringBuilder csv, params string[] fields)
    {
        // Quote every field; double embedded quotes and preserve embedded CR/LF.
        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0)
                csv.Append(',');
            csv.Append('"').Append(fields[i].Replace("\"", "\"\"")).Append('"');
        }
        csv.Append("\r\n");
    }
}
