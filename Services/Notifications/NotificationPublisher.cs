using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Services.Notifications;

// Called once by the existing Admin save action after a record is published.
// Normal edits do not rebroadcast; no database watcher or campaign table is used.
public sealed class NotificationPublisher(
    AppDbContext db,
    INotificationService notifications,
    IServiceScopeFactory scopes,
    ILogger<NotificationPublisher> logger)
{
    public Task PromotionPublishedAsync(Promotion promotion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(promotion);
        if (!promotion.IsActive || promotion.EndDate < DateOnly.FromDateTime(DateTime.Today))
            return Task.CompletedTask;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(promotion.PromotionId);
        return PublishAsync(NotificationTypes.Promotion, promotion.PromotionId,
            "New promotion: " + promotion.Title,
            $"{RestaurantFormatter.DiscountLabel(promotion)}. {promotion.StartDate:dd MMM yyyy} to {promotion.EndDate:dd MMM yyyy}. {promotion.Description}", ct);
    }

    public Task AnnouncementPublishedAsync(Announcement announcement, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        if (!announcement.IsLiveAt(DateTime.Now)) return Task.CompletedTask;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(announcement.AnnouncementId);
        return PublishAsync(NotificationTypes.Announcement, announcement.AnnouncementId,
            announcement.Title, announcement.Message, ct);
    }

    private async Task PublishAsync(string type, int sourceId, string title, string message, CancellationToken ct)
    {
        try
        {
            var audience = db.Set<UserAccount>().AsNoTracking()
                .Where(u => u.IsActive && u.Role == UserRoles.Customer);
            if (type == NotificationTypes.Promotion)
            {
                var previousCustomers = db.Set<Order>().Select(o => o.CustomerUserId)
                    .Union(db.Set<Reservation>().Select(r => r.CustomerUserId));
                audience = audience.Where(u => previousCustomers.Contains(u.UserId));
            }
            var recipients = await audience.Select(u => u.UserId).ToListAsync(ct);
            var staged = new List<Notification>();
            foreach (var userId in recipients)
            {
                staged.Add(await notifications.StageAsync(new NotificationInput
                {
                    UserId = userId, Type = type,
                    Title = Summary(title, 100), Message = Summary(message, 500)
                }, ct));
            }
            if (staged.Count == 0) return;
            await db.SaveChangesAsync(ct);

            // Separate scopes avoid concurrent use of a DbContext while keeping
            // large audiences from waiting on one provider call at a time.
            await Parallel.ForEachAsync(staged,
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
                async (notice, token) =>
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<INotificationDelivery>()
                        .DeliverAsync(notice.NotificationId, ct: token);
                });
        }
        catch (Exception ex)
        {
            // Publishing the business record already succeeded. No automatic
            // retry is attempted, so a provider failure cannot rebroadcast it.
            logger.LogError(ex, "Could not publish {Type} notifications for record {SourceId}.", type, sourceId);
        }
    }

    private static string Summary(string value, int limit)
    {
        value = value.Trim();
        if (value.Length <= limit) return value;
        // Avoid splitting a UTF-16 surrogate pair when shortening a message.
        var length = limit - 1;
        if (char.IsHighSurrogate(value[length - 1])) length--;
        return value[..length] + "…";
    }
}
