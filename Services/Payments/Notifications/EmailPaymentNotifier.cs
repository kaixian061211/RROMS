using RROMS.Web.Models.Payments;
using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using RROMS.Web.Services.Payments.Receipts;

namespace RROMS.Web.Services.Payments.Notifications;

/// <summary>
/// E-mails the customer when a payment is confirmed (with the PDF receipt attached) or refunded.
/// If Smtp:Host is empty it just logs, so the app still runs without an SMTP server.
/// </summary>
public class EmailPaymentNotifier : IPaymentNotifier
{
    private readonly SmtpSettings _smtp;
    private readonly IReceiptService _receipts;
    private readonly ILogger<EmailPaymentNotifier> _logger;

    public EmailPaymentNotifier(IOptions<SmtpSettings> options, IReceiptService receipts, ILogger<EmailPaymentNotifier> logger)
    {
        _smtp = options.Value;
        _receipts = receipts;
        _logger = logger;
    }

    public Task PaymentConfirmedAsync(PaymentDetails payment)
    {
        var subject = $"Payment received - order {payment.OrderLabel}";
        var body =
            $"<p>Hi {Encode(payment.CustomerName)},</p>" +
            $"<p>We've received your payment of <strong>{PaymentFormatter.Money(payment.Amount)}</strong> " +
            $"for order {Encode(payment.OrderLabel)} ({Encode(PaymentFormatter.MethodLabel(payment.Method))}).</p>" +
            $"<p>Your receipt <strong>{payment.ReceiptNo}</strong> is attached.</p>" +
            "<p>Thank you for dining with us.</p>";

        return SendAsync(payment, subject, body, _receipts.Generate(payment));
    }

    public Task PaymentRefundedAsync(PaymentDetails payment)
    {
        var subject = $"Refund processed - order {payment.OrderLabel}";
        var body =
            $"<p>Hi {Encode(payment.CustomerName)},</p>" +
            $"<p>We've refunded <strong>{PaymentFormatter.Money(payment.Amount)}</strong> for order {Encode(payment.OrderLabel)}.</p>" +
            "<p>Online refunds can take a few business days to reach your account.</p>";

        return SendAsync(payment, subject, body, attachment: null);
    }

    private async Task SendAsync(PaymentDetails payment, string subject, string htmlBody, byte[]? attachment)
    {
        if (string.IsNullOrWhiteSpace(payment.CustomerEmail))
        {
            _logger.LogInformation("No e-mail address for payment {ReceiptNo}; \"{Subject}\" was not sent", payment.ReceiptNo, subject);
            return;
        }

        if (!_smtp.Enabled)
        {
            _logger.LogInformation("E-mail is off (Smtp:Host is empty). Would have sent \"{Subject}\" to {To}", subject, payment.CustomerEmail);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromEmail));
        message.To.Add(new MailboxAddress(payment.CustomerName, payment.CustomerEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };
        if (attachment is not null)
        {
            builder.Attachments.Add($"RROMS-{payment.ReceiptNo}.pdf", attachment, ContentType.Parse("application/pdf"));
        }
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_smtp.Host, _smtp.Port, SecureSocketOptions.Auto);
        if (!string.IsNullOrWhiteSpace(_smtp.UserName))
        {
            await client.AuthenticateAsync(_smtp.UserName, _smtp.Password);
        }
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}