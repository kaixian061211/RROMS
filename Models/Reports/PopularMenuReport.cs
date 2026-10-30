namespace RROMS.Web.Models.Reports;

/// <summary>
/// Items purchased through currently paid orders, filtered by PaidAt.
/// Fully refunded purchases are excluded.
/// Amounts use saved order-item prices.
/// </summary>
public sealed record PopularMenuReport(
    int TotalQuantity,
    decimal TotalAmount,
    IReadOnlyList<PopularMenuItemSummary> Items,
    string Basis)
{
    public const int ChartCount = 10;

    /// <summary>
    /// The chart shows the first ten items; tables and exports retain all items.
    /// </summary>
    public IEnumerable<PopularMenuItemSummary> TopItems =>
        Items.Take(ChartCount);
}

public sealed record PopularMenuItemSummary(
    int Rank,
    int MenuItemId,
    string Name,
    string Category,
    int Quantity,
    decimal Amount,
    decimal? SharePercent);          // Share of total item amount.
