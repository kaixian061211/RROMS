using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Notifications;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Notifications;

[Authorize(Roles = PaymentRoles.Customer)]
public class IndexModel : PageModel
{
    private const int PageSize = 20;
    private readonly INotificationService _notifications;

    public IndexModel(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [BindProperty(SupportsGet = true)]
    public int? BeforeId { get; set; }

    public List<Notification> Notifications { get; private set; } = new();

    public int UnreadCount { get; private set; }

    public int? NextBeforeId { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.TryGetPaymentUserId(out var userId) || userId <= 0)
            return Forbid();

        if (!ModelState.IsValid || BeforeId is <= 0)
            return BadRequest();

        var rows = await _notifications.GetForUserAsync(
            userId,
            take: PageSize + 1,
            beforeId: BeforeId,
            ct: ct);

        Notifications = rows.Take(PageSize).ToList();

        if (rows.Count > PageSize)
            NextBeforeId = Notifications[^1].NotificationId;

        UnreadCount = await _notifications.GetUnreadCountAsync(userId, ct);

        return Page();
    }

    public async Task<IActionResult> OnPostReadAsync(
        int notificationId,
        CancellationToken ct)
    {
        if (!User.TryGetPaymentUserId(out var userId) || userId <= 0)
            return Forbid();

        if (!ModelState.IsValid || notificationId <= 0 || BeforeId is <= 0)
            return BadRequest();

        var updated = await _notifications.MarkAsReadAsync(
            userId,
            notificationId,
            ct);

        if (!updated)
            return NotFound();

        return RedirectToPage("./Index", new { beforeId = BeforeId });
    }

    public async Task<IActionResult> OnPostReadAllAsync(CancellationToken ct)
    {
        if (!User.TryGetPaymentUserId(out var userId) || userId <= 0)
            return Forbid();

        if (!ModelState.IsValid || BeforeId is <= 0)
            return BadRequest();

        await _notifications.MarkAllAsReadAsync(userId, ct);

        return RedirectToPage("./Index", new { beforeId = BeforeId });
    }

    public string TypeLabel(string type) => NotificationTypes.Label(type);
}