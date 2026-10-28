using Microsoft.EntityFrameworkCore;
using MimeKit;
using RROMS.Web.Data;
using RROMS.Web.Models.Notifications;

namespace RROMS.Web.Services.Notifications;

public sealed class NotificationDelivery : INotificationDelivery
{
    private readonly AppDbContext _db;
    private readonly INotificationRealtime _realtime;
    private readonly INotificationEmailSender _email;
    private readonly ILogger<NotificationDelivery> _logger;

    private readonly INotificationPushSender _push;

    public NotificationDelivery(
        AppDbContext db,
        INotificationRealtime realtime,
        INotificationEmailSender email,
        INotificationPushSender push,
        ILogger<NotificationDelivery> logger)
    {
        _db = db;
        _realtime = realtime;
        _email = email;
        _push = push;
        _logger = logger;
    }

    public async Task DeliverAsync(
        int notificationId,
        bool sendEmail = false,
        NotificationEmailRecipient? recipient = null,
        CancellationToken ct = default)
    {
        if (notificationId <= 0)
            throw new ArgumentOutOfRangeException(nameof(notificationId));

        if (_db.Database.CurrentTransaction is not null
            || System.Transactions.Transaction.Current is not null)
        {
            throw new InvalidOperationException(
                "Deliver notifications after the database transaction has committed.");
        }

        try
        {
            var notification = await _db.Notifications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    n => n.NotificationId == notificationId,
                    ct);

            if (notification is null)
            {
                _logger.LogWarning(
                    "Notification {NotificationId} was not found for delivery.",
                    notificationId);

                return;
            }

            await _realtime.PublishAsync(notification, ct);

            try
            {
                await _push.SendAsync(notification, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Web Push failed for notification {NotificationId}.",
                    notificationId);
            }

            if (!sendEmail)
                return;

            if (recipient is null
                || recipient.UserId != notification.UserId
                || !MailboxAddress.TryParse(recipient.Email, out var mailbox)
                || mailbox is null)
            {
                _logger.LogWarning(
                    "Email skipped for notification {NotificationId}: " +
                    "a valid recipient matching its owner is required.",
                    notificationId);

                return;
            }

            await _email.SendAsync(
                mailbox,
                notification.Title,
                notification.Message,
                ct);
        }
        catch (Exception ex)
        {
            // Delivery errors must not change the committed business result.
            _logger.LogError(
                ex,
                "Delivery failed for notification {NotificationId}; " +
                "its saved inbox record remains available.",
                notificationId);
        }
    }
}