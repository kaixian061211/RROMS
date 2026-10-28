using RROMS.Web.Models.Notifications;

namespace RROMS.Web.Services.Notifications;

public interface INotificationService
{
    // The caller saves the shared DbContext after staging.
    Task<Notification> StageAsync(
        NotificationInput input,
        CancellationToken ct = default);

    Task<List<Notification>> GetForUserAsync(
        int userId,
        int take = 30,
        int? beforeId = null,
        CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(
        int userId,
        CancellationToken ct = default);

    Task<bool> MarkAsReadAsync(
        int userId,
        int notificationId,
        CancellationToken ct = default);

    Task<int> MarkAllAsReadAsync(
        int userId,
        CancellationToken ct = default);
}