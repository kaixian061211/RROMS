using RROMS.Web.Models.Payments;

namespace RROMS.Web.Services.Payments;

/// <summary>
/// Every change to a payment goes through here. Business-rule problems are reported by
/// throwing <see cref="PaymentException"/> with a message that is safe to show.
/// An order has ONE payment row (unique index): a retry re-opens that row instead of adding another.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Customer checkout: creates (or re-opens) the order's payment and, for online methods, a Stripe
    /// Checkout page. <paramref name="baseUrl"/> is e.g. https://localhost:7001 (used for return links).
    /// </summary>
    Task<PaymentResult> StartCheckoutAsync(
        int orderId,
        int customerUserId,
        string method,
        string baseUrl);

    /// <summary>
    /// Validates a verified Stripe confirmation and saves its references.
    /// Returns true only for a new paid transition; duplicate/reference-only updates return false.
    /// </summary>
    Task<bool> MarkPaidOnlineAsync(OnlinePaymentConfirmation confirmation);

    /// <summary>
    /// Webhook / checkout error: the online payment failed or expired. Returns false if it was already handled.
    /// When <paramref name="sessionId"/> is given, only the attempt that owns that Stripe session is failed,
    /// so an old attempt's late "expired" event can't fail a newer attempt.
    /// </summary>
    Task<bool> MarkFailedAsync(int paymentId, string? sessionId = null);

    /// <summary>
    /// The customer came back from Stripe without paying.
    /// <paramref name="attempt"/> identifies which attempt the link belonged to.
    /// </summary>
    Task CancelOnlineAttemptAsync(
        int paymentId,
        int customerUserId,
        long? attempt = null);

    /// <summary>Staff took the money at the counter.</summary>
    Task CollectAtCounterAsync(int paymentId, int staffUserId, string method);

    /// <summary>Staff cancel a waiting counter payment (customer left, changed mind...).</summary>
    Task VoidPendingAsync(int paymentId, int staffUserId);

    /// <summary>
/// Staff refund in full, with an optional reason (max 200 characters).
/// Online payments are refunded through Stripe.
/// </summary>
Task RefundAsync(int paymentId, int staffUserId, string? reason = null);
}