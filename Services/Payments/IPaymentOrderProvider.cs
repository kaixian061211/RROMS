using RROMS.Web.Models.Payments;

namespace RROMS.Web.Services.Payments;

/// <summary>
/// The ONE place the payment module reads data that belongs to other modules (orders, customers, staff names).
/// The Payment table has no foreign keys to those modules, so nothing here assumes their schema.
/// Implement it against your real order/user data and register it in Program.cs.
/// </summary>
public interface IPaymentOrderProvider
{
    /// <summary>The order (with its customer and total), or null if it doesn't exist.</summary>
    Task<PaymentOrderInfo?> GetOrderAsync(int orderId);

    /// <summary>Orders placed by this customer.</summary>
    Task<IReadOnlyList<PaymentOrderInfo>> GetOrdersForCustomerAsync(int customerUserId);

    /// <summary>Several orders at once, keyed by order id. Unknown ids are simply missing.</summary>
    Task<IReadOnlyDictionary<int, PaymentOrderInfo>> GetOrdersAsync(IEnumerable<int> orderIds);

    /// <summary>Display names of staff members, keyed by user id. Unknown ids are simply missing.</summary>
    Task<IReadOnlyDictionary<int, string>> GetStaffNamesAsync(IEnumerable<int> staffUserIds);
}
