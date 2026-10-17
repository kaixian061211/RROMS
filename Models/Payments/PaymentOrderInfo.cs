namespace RROMS.Web.Models.Payments;

public class PaymentOrderInfo
{
    public int OrderId { get; set; }
    public int? ReservationId { get; set; }

    /// <summary>The numeric user id of the customer who placed the order.</summary>
    public int CustomerUserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>The amount to charge. The browser never supplies this.</summary>
    public decimal TotalAmount { get; set; }
}