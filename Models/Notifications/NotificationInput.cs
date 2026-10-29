namespace RROMS.Web.Models.Notifications;

public class NotificationInput
{
    public int UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public int? OrderId { get; set; }
    public int? ReservationId { get; set; }
    public int? PaymentId { get; set; }
}