using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using RROMS.Web.Services.Payments.Notifications;

namespace RROMS.Web.Services.Notifications;

public sealed class NotificationEmailSender : INotificationEmailSender
{
    private readonly SmtpSettings _smtp;
    private readonly ILogger<NotificationEmailSender> _logger;

    public NotificationEmailSender(
        IOptions<SmtpSettings> options,
        ILogger<NotificationEmailSender> logger)
    {
        _smtp = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string recipientEmail,
        string subject,
        string message,
        CancellationToken ct = default)
    {
        if (!_smtp.Enabled)
        {
            _logger.LogWarning(
                "Notification email was not sent because SMTP is not configured.");
            return;
        }

        var recipient = MailboxAddress.Parse(recipientEmail);

        if (!new EmailAddressAttribute().IsValid(recipient.Address))
        {
            throw new ArgumentException(
                "A full email address is required.",
                nameof(recipientEmail));
        }

        var email = new MimeMessage();

        email.From.Add(
            new MailboxAddress(_smtp.FromName, _smtp.FromEmail));

        email.To.Add(recipient);
        email.Subject = subject;
        email.Body = new TextPart("plain") { Text = message };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            _smtp.Host,
            _smtp.Port,
            SecureSocketOptions.Auto,
            ct);

        if (!string.IsNullOrWhiteSpace(_smtp.UserName))
        {
            await client.AuthenticateAsync(
                _smtp.UserName,
                _smtp.Password,
                ct);
        }

        await client.SendAsync(email, ct);
        await client.DisconnectAsync(true, ct);
    }
}