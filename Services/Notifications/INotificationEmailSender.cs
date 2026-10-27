namespace RROMS.Web.Services.Notifications;

public interface INotificationEmailSender
{
    Task SendAsync(
        string recipientEmail,
        string subject,
        string message,
        CancellationToken ct = default);
}