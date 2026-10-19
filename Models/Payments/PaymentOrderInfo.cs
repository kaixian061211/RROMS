namespace RROMS.Web.Models.Payments;

public class PaymentOrderInfo
{
    public int OrderId { get; set; }

    /// <summary>
    /// The customer's user ID, or null for a guest order.
    /// </summary>
    public int? CustomerUserId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>
    /// The final amount calculated by the ordering module.
    /// </summary>
    public decimal TotalAmount { get; set; }
}