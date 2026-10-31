using System.Text.Encodings.Web;
using System.Text.Json;
using RROMS.Web.Models.Reports;

namespace RROMS.Web.Pages.Reports;

/// <summary>Projects report results for charts without recalculating their metrics.</summary>
internal static class ReportChartData
{
    private static readonly JsonSerializerOptions ChartJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default
    };

    public static string Serialize(DashboardReport report)
    {
        var topItems = report.Menu.TopItems.ToArray();

        return JsonSerializer.Serialize(
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
                            .Select(m => ReportUi.PaymentMethodLabel(m.Method))
                            .ToArray(),

                        amounts = report.Sales.Methods
                            .Select(m => m.Amount)
                            .ToArray(),
                        shares = report.Sales.Methods.Select(m => m.SharePercent).ToArray(),
                        total = report.Sales.GrossCollections
                    }
                },

                orders = new
                {
                    status = new
                    {
                        labels = report.Orders.StatusBreakdown
                            .Select(s => ReportUi.OrderStatusLabel(s.Status))
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
                            .Select(s => ReportUi.ReservationStatusLabel(s.Status))
                            .ToArray(),

                        counts = report.Reservations.StatusBreakdown
                            .Select(s => s.Count)
                            .ToArray()
                    },

                    peak = new
                    {
                        labels = report.Reservations.PeakSlots
                            .Select(s => ReportUi.TimeLabel(s.TimeSlot))
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
                        .ToArray(),
                    amounts = topItems.Select(item => item.Amount).ToArray(),
                    categories = topItems.Select(item => item.Category).ToArray()
                }
            },
            ChartJsonOptions);
    }
}
