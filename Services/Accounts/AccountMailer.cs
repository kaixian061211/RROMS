using Microsoft.Extensions.Options;
using MimeKit;
using RROMS.Web.Services.Notifications;

namespace RROMS.Web.Services.Accounts;

/// <summary>Builds the message and hands it to the notification module's shared SMTP sender, so there is one mail setup for the whole app.</summary>
public sealed class AccountMailer : IAccountMailer
{
    private readonly ISmtpEmailSender _sender;
    private readonly SmtpSettings _settings;
    private readonly ILogger<AccountMailer> _logger;

    public AccountMailer(ISmtpEmailSender sender, IOptions<SmtpSettings> settings, ILogger<AccountMailer> logger)
    {
        _sender = sender;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        if (!_settings.Enabled)
        {
            _logger.LogWarning("An account e-mail to {To} was not sent because SMTP is not configured (Smtp:Host).", toEmail);
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            await _sender.SendAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send an account e-mail to {To}.", toEmail);
            return false;
        }
    }
}
