namespace RROMS.Web.Models.Payments;

/// <summary>
/// A flat, display-ready payment: the Payment row plus the customer and staff names supplied by
/// the other modules. Used by lists, details, receipts, e-mails and exports.
/// </summary>
public class PaymentDetails
{
    public int PaymentId { get; set; }
    public int? OrderId { get; set; }
    public int? ReservationId { get; set; }

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

    public string ReceiptNo => $"RCP-{PaymentId:D6}";

    public bool IsAwaitingCounter =>
        Status == PaymentStatus.Pending && Method == PaymentMethod.PayAtCounter;

    public bool HasReceipt => Status is PaymentStatus.Paid or PaymentStatus.Refunded;
}