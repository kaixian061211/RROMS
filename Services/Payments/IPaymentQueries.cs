using RROMS.Web.Models.Payments;

namespace RROMS.Web.Services.Payments;

/// <summary>Read-only access to payments. Nothing here changes data.</summary>
public interface IPaymentQueries
{
    Task<PaymentDetails?> GetDetailsAsync(int paymentId);

    /// <summary>The payment for an order (there is at most one), or null if the order has none yet.</summary>
    Task<PaymentDetails?> GetByOrderAsync(int orderId);

    /// <summary>The payments of several orders at once, keyed by order id.</summary>
    Task<Dictionary<int, PaymentDetails>> GetByOrdersAsync(IEnumerable<int> orderIds);

    /// <summary>All payments for orders placed by this customer, newest first.</summary>
    Task<List<PaymentDetails>> GetForCustomerAsync(int customerUserId);

    /// <summary>Pending "pay at counter" payments, oldest first - the cashier's queue.</summary>
    Task<List<PaymentDetails>> GetCounterQueueAsync();

    Task<List<PaymentDetails>> SearchAsync(PaymentFilter filter);
}
