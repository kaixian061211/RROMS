using Microsoft.AspNetCore.SignalR;
using RROMS.Web.Models.Notifications;

namespace RROMS.Web.Services.Notifications;

public sealed class NotificationRealtime : INotificationRealtime
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationRealtime> _logger;

    public NotificationRealtime(
        IHubContext<NotificationHub> hub,
        ILogger<NotificationRealtime> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task PublishAsync(
        Notification notification,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            notification.NotificationId);

        try
        {
            await _hub.Clients
                .Group(NotificationHub.GroupName(notification.UserId))
                .SendAsync("NotificationReceived", new
                {
                    notification.NotificationId,
                    notification.Title,
                    notification.Message
                }, ct);
        }
        catch (Exception ex)
        {
            // The saved inbox record remains available if live delivery fails.
            _logger.LogWarning(
                ex,
                "Could not send live update for notification {NotificationId}",
                notification.NotificationId);
        }
    }
}