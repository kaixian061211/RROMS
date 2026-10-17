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
}