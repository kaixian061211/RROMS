namespace RROMS.Web.Services.Notifications;

public interface INotificationRealtime
{
    // Call after the notification's database transaction has committed.
    Task PublishAsync(
        int notificationId,
        CancellationToken ct = default);
}