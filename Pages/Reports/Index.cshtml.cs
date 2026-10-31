using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Reports;
using RROMS.Web.Services.Reports;

namespace RROMS.Web.Pages.Reports;

[Authorize(Roles = PaymentRoles.Admin)]
[ResponseCache(
    Duration = 0,
    Location = ResponseCacheLocation.None,
    NoStore = true)]
public class IndexModel : PageModel
{
    public const int DetailRowLimit = 100;

    private readonly IReportService _reports;
    private readonly ReportExportService _exports;

    public IndexModel(IReportService reports, ReportExportService exports)
    {
        _reports = reports;
        _exports = exports;
    }

    [BindProperty(SupportsGet = true)]
    public ReportPeriod Period { get; set; } = ReportPeriod.Monthly;

    [BindProperty(SupportsGet = true)]
    public DateOnly ReferenceDate { get; set; } =
        DateOnly.FromDateTime(ReportDateRange.NowLocal());

    public DashboardReport? Report { get; private set; }

    public string ChartJson { get; private set; } = "{}";

    public bool HasActivity =>
        Report is { } report &&
        (
            report.Sales.PaymentCount > 0 ||
            report.Sales.RefundCount > 0 ||
            report.Orders.OrdersPlaced > 0 ||
            report.Reservations.ScheduledReservations > 0 ||
            report.Reservations.NewBookingsCreated > 0
        );

    public async Task OnGetAsync()
    {
        if (!TryCreateFilter(out var filter))
            return;

        var report = await _reports.BuildAsync(
            filter,
            HttpContext.RequestAborted);

        Report = report;

        ChartJson = ReportChartData.Serialize(report);
    }

    public Task<IActionResult> OnGetExportPdfAsync() =>
        ExportAsync(_exports.ExportPdf, "application/pdf", "pdf");

    public Task<IActionResult> OnGetExportExcelAsync() =>
        ExportAsync(_exports.ExportExcel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx");

    public Task<IActionResult> OnGetExportCsvAsync() =>
        ExportAsync(_exports.ExportCsv, "text/csv; charset=utf-8", "csv");

    private async Task<IActionResult> ExportAsync(
        Func<DashboardReport, byte[]> render, string contentType, string extension)
    {
        if (!TryCreateFilter(out var filter))
            return BadRequest(ModelState);

        var report = await _reports.BuildAsync(filter, HttpContext.RequestAborted);
        HttpContext.RequestAborted.ThrowIfCancellationRequested();
        return File(render(report), contentType, ReportExportService.FileName(report, extension));
    }

    private bool TryCreateFilter(out ReportFilter filter)
    {
        filter = new ReportFilter(Period, ReferenceDate);
        if (!ModelState.IsValid)
            return false;

        try
        {
            _ = ReportDateRange.Create(filter);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(string.Empty,
                $"Choose a valid period and a date between {ReportDateRange.FirstYear} and {ReportDateRange.LastYear}.");
            return false;
        }
    }
}
