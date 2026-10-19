using RROMS.Web.Models.Payments;

namespace RROMS.Web.Services.Payments.Notifications;

/// <summary>
/// Records payment confirmation and refund events in application logs.
/// </summary>
public class LoggingPaymentNotifier : IPaymentNotifier
{
    private readonly ILogger<LoggingPaymentNotifier> _logger;

    public LoggingPaymentNotifier(
        ILogger<LoggingPaymentNotifier> logger)
    {
        _logger = logger;
    }

    public Task PaymentConfirmedAsync(PaymentDetails payment)
    {
        _logger.LogInformation(
            "Payment {ReceiptNo} confirmed: {Amount} for order {OrderId} ({Customer})",
            payment.ReceiptNo,
            PaymentFormatter.Money(payment.Amount),
            payment.OrderLabel,
            payment.CustomerName);

        return Task.CompletedTask;
    }

    public Task PaymentRefundedAsync(PaymentDetails payment)
    {
        _logger.LogInformation(
            "Payment {ReceiptNo} refunded: {Amount} for order {OrderId} ({Customer})",
            payment.ReceiptNo,
            PaymentFormatter.Money(payment.Amount),
            payment.OrderLabel,
            payment.CustomerName);

        return Task.CompletedTask;
    }
}