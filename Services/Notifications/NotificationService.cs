using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Notification> StageAsync(
        NotificationInput input,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        CheckUserId(input.UserId);

        if (string.IsNullOrWhiteSpace(input.Title)
            || input.Title.Trim().Length > 100)
        {
            throw new ArgumentException(
                "A title of up to 100 characters is required.",
                nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.Message)
            || input.Message.Trim().Length > 500)
        {
            throw new ArgumentException(
                "A message of up to 500 characters is required.",
                nameof(input));
        }

        if (!NotificationTypes.HasValidRelatedRecord(input))
        {
            throw new ArgumentException(
                "The notification type and related record do not match.",
                nameof(input));
        }

        if (!await OwnsRelatedRecordAsync(input, ct))
        {
            throw new InvalidOperationException(
                "The related record was not found for this user.");
        }

        var notification = new Notification
        {
            UserId = input.UserId,
            Type = input.Type,
            Title = input.Title.Trim(),
            Message = input.Message.Trim(),
            OrderId = input.OrderId,
            ReservationId = input.ReservationId,
            PaymentId = input.PaymentId
            // IsRead and CreatedAt use the database defaults.
        };

        _db.Notifications.Add(notification);

        return notification;
    }

    public async Task<List<Notification>> GetForUserAsync(
        int userId,
        int take = 30,
        int? beforeId = null,
        CancellationToken ct = default)
    {
        CheckUserId(userId);

        if (beforeId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(beforeId));

        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (beforeId is int cursor)
        {
            query = query.Where(n => n.NotificationId < cursor);
        }

        return await query
            .OrderByDescending(n => n.NotificationId)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);
    }

    public Task<int> GetUnreadCountAsync(
        int userId,
        CancellationToken ct = default)
    {
        CheckUserId(userId);

        return _db.Notifications.CountAsync(
            n => n.UserId == userId && !n.IsRead,
            ct);
    }

    public async Task<bool> MarkAsReadAsync(
        int userId,
        int notificationId,
        CancellationToken ct = default)
    {
        CheckUserId(userId);

        if (notificationId <= 0)
            throw new ArgumentOutOfRangeException(nameof(notificationId));

        var updated = await _db.Notifications
            .Where(n => n.NotificationId == notificationId
                && n.UserId == userId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(n => n.IsRead, true),
                ct);

        return updated > 0;
    }

    public Task<int> MarkAllAsReadAsync(
        int userId,
        CancellationToken ct = default)
    {
        CheckUserId(userId);

        return _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(
                s => s.SetProperty(n => n.IsRead, true),
                ct);
    }

    private async Task<bool> OwnsRelatedRecordAsync(
        NotificationInput input,
        CancellationToken ct)
    {
        return input.Type switch
        {
            NotificationTypes.Order =>
                await _db.Set<Order>().AnyAsync(
                    o => o.OrderId == input.OrderId
                        && o.CustomerUserId == input.UserId,
                    ct),

            NotificationTypes.Reservation =>
                await _db.Set<Reservation>().AnyAsync(
                    r => r.ReservationId == input.ReservationId
                        && r.CustomerUserId == input.UserId,
                    ct),

            NotificationTypes.Payment =>
                await (
                    from payment in _db.Payments
                    join order in _db.Set<Order>()
                        on payment.OrderId equals order.OrderId
                    where payment.PaymentId == input.PaymentId
                        && order.CustomerUserId == input.UserId
                    select payment.PaymentId
                ).AnyAsync(ct),

            _ => true
        };
    }

    private static void CheckUserId(int userId)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));
    }
}