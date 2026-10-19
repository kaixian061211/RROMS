using RROMS.Web.Models.Payments;

namespace RROMS.Web.Services.Payments;

public interface IPaymentService
{
    Task<Payment> CreateCounterPaymentAsync(int orderId);

    Task<decimal> CollectAtCounterAsync(
        int paymentId,
        string method,
        decimal? cashReceived = null);

    Task VoidPendingAsync(int paymentId);

    Task RefundCounterPaymentAsync(
        int paymentId,
        string reason);
}