using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace RROMS.Web.Services.Notifications;

public sealed class SmtpEmailSender : ISmtpEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<SmtpSettings> options,
        ILogger<SmtpEmailSender> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        MimeMessage message,
        CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogWarning(
                "Email was not sent because SMTP is not configured.");
            return;
        }

        if (message.From.Count == 0)
        {
            message.From.Add(
                new MailboxAddress(_settings.FromName, _settings.FromEmail));
        }

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _settings.Host,
            _settings.Port,
            SecureSocketOptions.Auto,
            ct);

        if (!string.IsNullOrWhiteSpace(_settings.UserName))
        {
            await client.AuthenticateAsync(
                _settings.UserName,
                _settings.Password,
                ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
