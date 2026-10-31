namespace RROMS.Web.Pages.Reports;

public sealed record ReportCell(
    string Text,
    string CssClass = "",
    bool Numeric = false);

public sealed record ReportTableModel(
    string Caption,
    string[] Headers,
    IEnumerable<ReportCell[]> Rows,
    string EmptyMessage);
