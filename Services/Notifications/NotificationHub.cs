using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;
using RROMS.Web.Models.Tracking;
using RROMS.Web.Services.Tracking;

namespace RROMS.Web.Services.Notifications;

[Authorize(Roles = PaymentRoles.Customer)]
public sealed class NotificationHub : Hub
{
    private readonly INotificationService _notifications;
    private readonly TrackingQueries _tracking;

    public NotificationHub(
        INotificationService notifications,
        TrackingQueries tracking)
    {
        _notifications = notifications;
        _tracking = tracking;
    }

    internal static string GroupName(int userId) =>
        "notifications:user:" + userId.ToString(CultureInfo.InvariantCulture);

    public override async Task OnConnectedAsync()
    {
        if (!TryGetUserId(out var userId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GroupName(userId));

        await base.OnConnectedAsync();
    }

    public Task<int> GetUnreadCount()
    {
        if (!TryGetUserId(out var userId))
            throw new HubException("A valid customer login is required.");

        return _notifications.GetUnreadCountAsync(
            userId,
            Context.ConnectionAborted);
    }

    public async Task<TrackingSnapshot?> GetTrackingStatus(
        int? orderId = null,
        int? reservationId = null,
        int? paymentId = null)
    {
        if (!TryGetUserId(out var userId))
            throw new HubException("A valid customer login is required.");

        try
        {
            return await _tracking.GetAsync(
                userId,
                orderId,
                reservationId,
                paymentId,
                Context.ConnectionAborted);
        }
        catch (ArgumentException)
        {
            throw new HubException(
                "Provide exactly one positive order, reservation or payment ID.");
        }
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;

        return Context.User is { } user
            && user.TryGetPaymentUserId(out userId)
            && userId > 0;
    }
}