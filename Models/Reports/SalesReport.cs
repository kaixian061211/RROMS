namespace RROMS.Web.Models.Reports;

/// <summary>
/// Collections use PaidAt; confirmed refunds use RefundedAt.
/// A refunded payment remains in its original collection period.
/// </summary>
public sealed record SalesReport(
    decimal GrossCollections,
    int PaymentCount,
    decimal ConfirmedRefunds,
    int RefundCount,
    decimal NetCollections,
    decimal? AveragePayment,
    IReadOnlyList<SalesTrendPoint> Trend,
    IReadOnlyList<PaymentMethodSummary> Methods,
    IReadOnlyList<SalesTransaction> Transactions,
    string Basis);

public sealed record SalesTrendPoint(
    string Label,
    decimal Collections,
    int PaymentCount,
    decimal Refunds,
    decimal Net);

public sealed record PaymentMethodSummary(
    string Method,
    int PaymentCount,
    decimal Amount,
    decimal Refunds,
    decimal Net,
    decimal? SharePercent);          // Share of gross collections.

public enum SalesTransactionType
{
    Payment,
    Refund
}

/// <summary>
/// One money movement: collections are positive; refunds are negative.
/// Ledger amounts add up to net collections.
/// </summary>
public sealed record SalesTransaction(
    DateTime At,                    // Restaurant-local time.
    SalesTransactionType Type,
    int PaymentId,
    int OrderId,
    int? CustomerUserId,             // Null if the order cannot be found.
    string Method,
    decimal Amount,
    string? Reference,
    string? Note);
