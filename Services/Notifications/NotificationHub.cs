using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Services.Notifications;

[Authorize(Roles = PaymentRoles.Customer)]
public sealed class NotificationHub : Hub
{
    private readonly INotificationService _notifications;

    public NotificationHub(INotificationService notifications)
    {
        _notifications = notifications;
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

    private bool TryGetUserId(out int userId)
    {
        userId = 0;

        return Context.User is { } user
            && user.TryGetPaymentUserId(out userId)
            && userId > 0;
    }
}