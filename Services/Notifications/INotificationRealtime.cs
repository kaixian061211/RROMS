using RROMS.Web.Models.Notifications;

namespace RROMS.Web.Services.Notifications;

public interface INotificationRealtime
{
    // Call after the notification's database transaction has committed.
    Task PublishAsync(
        Notification notification,
        CancellationToken ct = default);
}