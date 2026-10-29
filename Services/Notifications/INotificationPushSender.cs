using RROMS.Web.Models.Notifications;

namespace RROMS.Web.Services.Notifications;

public interface INotificationPushSender
{
    Task SendAsync(
        Notification notification,
        CancellationToken ct = default);
}