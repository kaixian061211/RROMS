using OrderStatuses = RROMS.Web.Models.OrdersAndReservations.OrderStatus;

namespace RROMS.Web.Models.Payments;

/// <summary>
/// Payment information enriched with customer and staff details.
/// Used by payment lists, receipts, reports and exports.
/// </summary>
public class PaymentDetails
{
    public int PaymentId { get; set; }
    public int OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = "-";
    public string CustomerEmail { get; set; } = string.Empty;

    public int? RecordedByStaffId { get; set; }
    public string StaffName { get; set; } = "-";

    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    public string? TransactionRef { get; set; }
    public string? StripePaymentIntentId { get; set; }

    public DateTime? RefundedAt { get; set; }
    public int? RefundedByStaffId { get; set; }
    public string? RefundReason { get; set; }

    public string ReceiptNo => $"RCP-{PaymentId:D6}";

    public string OrderLabel => $"#{OrderId}";

    public bool IsAwaitingCounter =>
        Status == PaymentStatus.Pending &&
        Method == PaymentMethod.PayAtCounter;

    public bool CanCollectAtCounter =>
        IsAwaitingCounter && OrderStatus != OrderStatuses.Cancelled;

    public bool HasReceipt =>
        Status is PaymentStatus.Paid or PaymentStatus.Refunded;
}