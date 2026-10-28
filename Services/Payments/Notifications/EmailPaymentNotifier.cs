using RROMS.Web.Models.Payments;
using System.Net;
using MimeKit;
using RROMS.Web.Services.Payments.Receipts;
using RROMS.Web.Services.Notifications;

namespace RROMS.Web.Services.Payments.Notifications;

/// <summary>
/// E-mails the customer when a payment is confirmed (with the PDF receipt attached) or refunded.
/// The shared SMTP sender handles transport and configuration.
/// </summary>
public sealed class EmailPaymentNotifier : IPaymentNotifier
{
    private readonly IReceiptService _receipts;
    private readonly ISmtpEmailSender _smtp;
    private readonly ILogger<EmailPaymentNotifier> _logger;

    public EmailPaymentNotifier(
        IReceiptService receipts,
        ISmtpEmailSender smtp,
        ILogger<EmailPaymentNotifier> logger)
    {
        _receipts = receipts;
        _smtp = smtp;
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

    private async Task SendAsync(
        PaymentDetails payment,
        string subject,
        string htmlBody,
        byte[]? attachment)
    {
        if (string.IsNullOrWhiteSpace(payment.CustomerEmail))
        {
            _logger.LogInformation("No e-mail address for payment {ReceiptNo}; \"{Subject}\" was not sent", payment.ReceiptNo, subject);
            return;
        }

        var message = new MimeMessage();
        message.To.Add(new MailboxAddress(payment.CustomerName, payment.CustomerEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };
        if (attachment is not null)
        {
            builder.Attachments.Add(
                $"RROMS-{payment.ReceiptNo}.pdf",
                attachment,
                ContentType.Parse("application/pdf"));
        }
        message.Body = builder.ToMessageBody();

        await _smtp.SendAsync(message);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}