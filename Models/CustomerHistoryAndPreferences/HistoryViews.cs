namespace RROMS.Web.Models.CustomerHistoryAndPreferences;

/// <summary>A reservation in someone's history. SpecialRequest is the customer's own request on the booking.</summary>
public record ReservationHistoryItem(
    int ReservationId,
    DateOnly Date,
    TimeOnly Time,
    int PartySize,
    string Status,
    string? SpecialRequest);

/// <summary>An order in a customer's history. Payment is already worded for the page.</summary>
public record OrderHistoryItem(
    int OrderId,
    string Status,
    DateTime CreatedAt,
    decimal Total,
    string ItemsSummary,
    string PaymentLabel,
    string PaymentBadge);

/// <summary>
/// A payment in a customer's history. It leaves out the gateway's references on purpose,
/// so a page can't show them by mistake.
/// </summary>
public record PaymentHistoryItem(
    int PaymentId,
    string ReceiptNo,
    string OrderLabel,
    string Method,
    string Status,
    decimal Amount,
    DateTime? PaidAt,
    bool HasReceipt);

/// <summary>Everything the customer's own history page shows.</summary>
public record CustomerHistory(
    IReadOnlyList<ReservationHistoryItem> Reservations,
    IReadOnlyList<OrderHistoryItem> Orders,
    IReadOnlyList<PaymentHistoryItem> Payments)
{
    public static CustomerHistory Empty { get; } = new(
        Array.Empty<ReservationHistoryItem>(),
        Array.Empty<OrderHistoryItem>(),
        Array.Empty<PaymentHistoryItem>());
}
