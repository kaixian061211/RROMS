using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;

namespace RROMS.Web.Services.Notifications;

public sealed class NotificationRealtime : INotificationRealtime
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationRealtime> _logger;

    public NotificationRealtime(
        AppDbContext db,
        IHubContext<NotificationHub> hub,
        ILogger<NotificationRealtime> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task PublishAsync(
        int notificationId,
        CancellationToken ct = default)
    {
        if (notificationId <= 0)
            throw new ArgumentOutOfRangeException(nameof(notificationId));

        if (_db.Database.CurrentTransaction is not null
            || System.Transactions.Transaction.Current is not null)
        {
            throw new InvalidOperationException(
                "Publish notifications after the database transaction has committed.");
        }

        try
        {
            var notification = await _db.Notifications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    n => n.NotificationId == notificationId,
                    ct);

            if (notification is null)
                return;

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
                notificationId);
        }
    }
}