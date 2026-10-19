using RROMS.Web.Models.Payments;

namespace RROMS.Web.Services.Payments.Notifications;

/// <summary>
/// Defines notification actions after a payment is confirmed or refunded.
/// </summary>
public interface IPaymentNotifier
{
    Task PaymentConfirmedAsync(PaymentDetails payment);

    Task PaymentRefundedAsync(PaymentDetails payment);
}