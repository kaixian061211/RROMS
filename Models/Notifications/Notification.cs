using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.Notifications;

public class Notification
{
    public int NotificationId { get; set; }

    // The user account receiving this notification.
    public int UserId { get; set; }

    [Required, StringLength(20)]
    public string Type { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    public int? OrderId { get; set; }
    public int? ReservationId { get; set; }
    public int? PaymentId { get; set; }
}
