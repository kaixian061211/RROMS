using Microsoft.EntityFrameworkCore;
using MimeKit;
using RROMS.Web.Data;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Models.Accounts;

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

            // Resolve the affected customer, never the currently signed-in staff
            // member. Explicit trusted recipients remain supported for callers.
            if (sendEmail && (recipient is null || string.IsNullOrWhiteSpace(recipient.Email)))
            {
                recipient = await _db.Set<UserAccount>().AsNoTracking()
                    .Where(u => u.UserId == notification.UserId && u.IsActive && u.Role == UserRoles.Customer)
                    .Select(u => new NotificationEmailRecipient(u.UserId, u.Email))
                    .SingleOrDefaultAsync(ct);
            }

            MailboxAddress? mailbox = null;
            if (sendEmail && (recipient is null || recipient.UserId != notification.UserId
                || !MailboxAddress.TryParse(recipient.Email, out mailbox)
                || mailbox is null))
            {
                mailbox = null;
                _logger.LogWarning(
                    "Email skipped for notification {NotificationId}: " +
                    "a valid recipient matching its owner is required.",
                    notificationId);
            }

            // Providers are independent and do not use the DbContext. Start all
            // eligible channels together, with a separate deadline for each.
            var deliveries = new List<Task>
            {
                DeliverChannelAsync("SignalR", notificationId,
                    token => _realtime.PublishAsync(notification, token), ct),
                DeliverChannelAsync("Web Push", notificationId,
                    token => _push.SendAsync(notification, token), ct)
            };
            if (sendEmail && mailbox is not null)
            {
                deliveries.Add(DeliverChannelAsync("Email", notificationId,
                    token => _email.SendAsync(mailbox, notification.Title, notification.Message, token), ct));
            }

            await Task.WhenAll(deliveries);
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

    private async Task DeliverChannelAsync(
        string channel, int notificationId,
        Func<CancellationToken, Task> send, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await send(deadline.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "{Channel} delivery failed or timed out for notification {NotificationId}; its inbox record remains available.",
                channel, notificationId);
        }
    }
}
