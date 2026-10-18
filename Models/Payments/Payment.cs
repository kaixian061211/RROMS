using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace RROMS.Web.Models.Payments;

public class Payment
{
    public int PaymentId { get; set; }

    public int? ReservationId { get; set; }         
    public int? OrderId { get; set; }               
    public int? RecordedByStaffId { get; set; }     
    [Precision(10, 2)]
    public decimal Amount { get; set; }

    [Required, StringLength(20)]
    public string PaymentMethod { get; set; } = string.Empty;   

    [Required, StringLength(20)]
    public string PaymentStatus { get; set; } = "pending";      

    public DateTime CreatedAt { get; set; }         
    public DateTime? PaidAt { get; set; }
    public string? TransactionRef { get; set; }

    /// <summary>A deposit is a reservation-only payment. The final bill is a separate row with an OrderId.</summary>
    public bool IsDeposit => OrderId is null;

    [StringLength(100)]
    public string? StripePaymentIntentId { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? RefundedAt { get; set; }
    public int? RefundedByStaffId { get; set; }

    [StringLength(200)]
    public string? RefundReason { get; set; }

    [StringLength(100)]
    public string? StripeRefundId { get; set; }
}