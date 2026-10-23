namespace RROMS.Web.Models.Payments;

/// <summary>Payment details received from a verified Stripe Checkout webhook.</summary>
public record OnlinePaymentConfirmation(
    int PaymentId,
    int OrderId,
    string SessionId,
    string? PaymentIntentId,
    long AmountTotal,
    string Currency,
    long? AttemptTicks);