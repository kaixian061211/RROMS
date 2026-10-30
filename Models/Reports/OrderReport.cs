namespace RROMS.Web.Models.Reports;

/// <summary>
/// Orders are counted by their creation date.
/// Order value and its average exclude cancelled orders.
/// </summary>
public sealed record OrderReport(
    int OrdersPlaced,
    int CancelledOrders,
    decimal OrderValue,
    decimal? AverageOrderValue,
    IReadOnlyList<StatusCount> StatusBreakdown,
    IReadOnlyList<OrderTrendPoint> Trend,
    IReadOnlyList<OrderRow> Orders,
    string Basis);

public sealed record OrderTrendPoint(
    string Label,
    int OrdersPlaced,
    decimal OrderValue);

public sealed record OrderRow(
    int OrderId,
    DateTime PlacedAt,               // Restaurant-local time.
    int CustomerUserId,
    string Table,
    string Status,
    int ItemCount,
    decimal Total,                  // Saved item prices times quantities.
    string? PaymentStatus);         // Null if no payment exists.
