using MimeKit;

namespace RROMS.Web.Services.Notifications;

public interface ISmtpEmailSender
{
    Task SendAsync(MimeMessage message, CancellationToken ct = default);
}
