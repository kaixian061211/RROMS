using RROMS.Web.Models.Reports;

namespace RROMS.Web.Services.Reports;

/// <summary>Builds the shared result used by report pages and exports.</summary>
public interface IReportService
{
    Task<DashboardReport> BuildAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default);
}
