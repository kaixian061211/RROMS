using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
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
    private static readonly CultureInfo ReportCulture =
        CultureInfo.GetCultureInfo("en-MY");

    public const int DetailRowLimit = 100;

    private static readonly JsonSerializerOptions ChartJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default
    };

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

        var topItems = report.Menu.TopItems.ToArray();

        ChartJson = JsonSerializer.Serialize(
            new
            {
                currency = DashboardReport.Currency,

                sales = new
                {
                    trend = new
                    {
                        labels = report.Sales.Trend
                            .Select(p => p.Label)
                            .ToArray(),

                        collections = report.Sales.Trend
                            .Select(p => p.Collections)
                            .ToArray(),

                        refunds = report.Sales.Trend
                            .Select(p => p.Refunds)
                            .ToArray(),

                        net = report.Sales.Trend
                            .Select(p => p.Net)
                            .ToArray()
                    },

                    methods = new
                    {
                        labels = report.Sales.Methods
                            .Select(m => PaymentMethodLabel(m.Method))
                            .ToArray(),

                        amounts = report.Sales.Methods
                            .Select(m => m.Amount)
                            .ToArray()
                    }
                },

                orders = new
                {
                    status = new
                    {
                        labels = report.Orders.StatusBreakdown
                            .Select(s => OrderStatusLabel(s.Status))
                            .ToArray(),

                        counts = report.Orders.StatusBreakdown
                            .Select(s => s.Count)
                            .ToArray()
                    },

                    trend = new
                    {
                        labels = report.Orders.Trend
                            .Select(p => p.Label)
                            .ToArray(),

                        counts = report.Orders.Trend
                            .Select(p => p.OrdersPlaced)
                            .ToArray(),

                        values = report.Orders.Trend
                            .Select(p => p.OrderValue)
                            .ToArray()
                    }
                },

                reservations = new
                {
                    trend = new
                    {
                        labels = report.Reservations.Trend
                            .Select(p => p.Label)
                            .ToArray(),

                        scheduled = report.Reservations.Trend
                            .Select(p => p.Scheduled)
                            .ToArray(),

                        cancelled = report.Reservations.Trend
                            .Select(p => p.Cancelled)
                            .ToArray()
                    },

                    status = new
                    {
                        labels = report.Reservations.StatusBreakdown
                            .Select(s => ReservationStatusLabel(s.Status))
                            .ToArray(),

                        counts = report.Reservations.StatusBreakdown
                            .Select(s => s.Count)
                            .ToArray()
                    },

                    peak = new
                    {
                        labels = report.Reservations.PeakSlots
                            .Select(s => TimeLabel(s.TimeSlot))
                            .ToArray(),

                        counts = report.Reservations.PeakSlots
                            .Select(s => s.Reservations)
                            .ToArray(),

                        isPeak = report.Reservations.PeakSlots
                            .Select(s => s.IsPeak)
                            .ToArray()
                    }
                },

                menu = new
                {
                    labels = topItems
                        .Select(item => item.Name)
                        .ToArray(),

                    quantities = topItems
                        .Select(item => item.Quantity)
                        .ToArray()
                }
            },
            ChartJsonOptions);
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

    public string Money(decimal? amount) =>
        amount.HasValue
            ? $"{DashboardReport.Currency} {amount.Value.ToString("N2", ReportCulture)}"
            : "N/A";

    public string Count(int value) =>
        value.ToString("N0", ReportCulture);

    public string Percentage(decimal? value) =>
        value.HasValue
            ? $"{value.Value.ToString("N1", ReportCulture)}%"
            : "N/A";

    public string Timestamp(DateTime value) =>
        value.ToString("d MMM yyyy, HH:mm", ReportCulture);

    public string PaymentMethodLabel(string method) =>
        method switch
        {
            PaymentMethod.Cash => "Cash",
            PaymentMethod.Card => "Card",
            PaymentMethod.OnlineBanking => "Online banking",
            PaymentMethod.EWallet => "E-wallet",
            PaymentMethod.PayAtCounter => "Pay at counter",
            _ => method
        };

    public string CustomerLabel(int? customerId) =>
        customerId.HasValue
            ? $"Customer #{customerId.Value}"
            : "Unknown";

    public string OrderStatusLabel(string status) =>
        status switch
        {
            OrderStatus.Pending => "Pending",
            OrderStatus.Preparing => "Preparing",
            OrderStatus.Ready => "Ready",
            OrderStatus.Served => "Served",
            OrderStatus.Completed => "Completed",
            OrderStatus.Cancelled => "Cancelled",
            _ => status
        };

    public string PaymentStatusLabel(string? status) =>
        status switch
        {
            PaymentStatus.Pending => "Pending",
            PaymentStatus.Paid => "Paid",
            PaymentStatus.Failed => "Failed",
            PaymentStatus.Refunded => "Refunded",
            null => "Not recorded",
            _ => status ?? "Unknown"
        };

    public string ReservationStatusLabel(string status) =>
        status switch
        {
            ReservationStatus.Pending => "Pending",
            ReservationStatus.Confirmed => "Confirmed",
            ReservationStatus.Cancelled => "Cancelled",
            ReservationStatus.Completed => "Completed",
            _ => status
        };

    public string DateLabel(DateOnly value) =>
        value.ToString("d MMM yyyy", ReportCulture);

    public string TimeLabel(TimeOnly value) =>
        value.ToString("HH:mm", ReportCulture);
}
