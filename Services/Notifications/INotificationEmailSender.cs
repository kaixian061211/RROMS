using MimeKit;

namespace RROMS.Web.Services.Notifications;

public interface INotificationEmailSender
{
    Task SendAsync(
        MailboxAddress recipient,
        string subject,
        string message,
        CancellationToken ct = default);
}