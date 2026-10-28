using RROMS.Web.Models.Notifications;

namespace RROMS.Web.Services.Notifications;

public interface INotificationRealtime
{
    Task PublishAsync(
        Notification notification,
        CancellationToken ct = default);

    Task PublishReadStateChangedAsync(
        int userId,
        CancellationToken ct = default);
}