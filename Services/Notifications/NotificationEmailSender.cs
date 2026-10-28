using MimeKit;

namespace RROMS.Web.Services.Notifications;

public sealed class NotificationEmailSender : INotificationEmailSender
{
    private readonly ISmtpEmailSender _smtp;

    public NotificationEmailSender(ISmtpEmailSender smtp)
    {
        _smtp = smtp;
    }

    public Task SendAsync(
        MailboxAddress recipient,
        string subject,
        string message,
        CancellationToken ct = default)
    {
        var email = new MimeMessage();
        email.To.Add(recipient);
        email.Subject = subject;
        email.Body = new TextPart("plain") { Text = message };

        return _smtp.SendAsync(email, ct);
    }
}