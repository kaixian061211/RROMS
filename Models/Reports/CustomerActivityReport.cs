namespace RROMS.Web.Models.Reports;

/// <summary>
/// Account activity from bookings created, orders placed,
/// collections and refunds in the selected period.
/// Browsing activity is excluded.
/// </summary>
public sealed record CustomerActivityReport(
    int ActiveCustomers,
    int ReturningCustomers,
    IReadOnlyList<CustomerActivitySummary> Customers,
    string Basis);

public sealed record CustomerActivitySummary(
    int CustomerUserId,
    string DisplayName,
    int ReservationsMade,
    int OrdersPlaced,
    int PaidOrders,
    decimal GrossSpending,
    decimal RefundsReceived,
    decimal NetSpending,
    int EstimatedDiningDays,
    bool IsReturning);              // Two or more estimated dining days.
