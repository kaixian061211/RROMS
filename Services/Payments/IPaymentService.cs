using RROMS.Web.Models.Payments;

namespace RROMS.Web.Services.Payments;

/// <summary>
/// Every change to a payment goes through here.
/// Business-rule problems throw PaymentException with a safe message.
/// An order has one payment row: a retry reopens that row.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Creates or reopens an order payment and, for online methods,
    /// creates a Stripe Checkout page.
    /// </summary>
    Task<PaymentResult> StartCheckoutAsync(
        int orderId,
        int customerUserId,
        string method,
        string baseUrl);

    /// <summary>
    /// Validates a verified Stripe confirmation and saves its references.
    /// Returns true only for a new paid transition.
    /// </summary>
    Task<bool> MarkPaidOnlineAsync(
        OnlinePaymentConfirmation confirmation);

    /// <summary>
    /// Marks an online attempt failed or expired.
    /// A supplied Session ID must match the current attempt.
    /// </summary>
    Task<bool> MarkFailedAsync(
        int paymentId,
        string? sessionId = null);

    /// <summary>
    /// Cancels the customer's current online attempt.
    /// </summary>
    Task CancelOnlineAttemptAsync(
        int paymentId,
        int customerUserId,
        long? attempt = null);

    /// <summary>Staff took the money at the counter.</summary>
    Task CollectAtCounterAsync(
        int paymentId,
        int staffUserId,
        string method);

    /// <summary>Staff cancel a waiting counter payment.</summary>
    Task VoidPendingAsync(
        int paymentId,
        int staffUserId);

    /// <summary>
    /// Refunds in full, with an optional reason of at most 200 characters.
    /// Online payments are refunded through Stripe.
    /// </summary>
    Task RefundAsync(
        int paymentId,
        int staffUserId,
        string? reason = null);

    /// <summary>
    /// Checks an existing Stripe refund without submitting another request.
    /// </summary>
    Task RecheckRefundAsync(
        int paymentId,
        int staffUserId,
        string? reason = null);
}