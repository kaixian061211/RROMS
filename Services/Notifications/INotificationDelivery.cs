using RROMS.Web.Models.Notifications;

namespace RROMS.Web.Services.Notifications;

public interface INotificationDelivery
{
    // Call once after the notification's transaction has committed.
    Task DeliverAsync(
        int notificationId,
        bool sendEmail = false,
        NotificationEmailRecipient? recipient = null,
        CancellationToken ct = default);
}