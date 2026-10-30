using RROMS.Web.Models.CustomerHistoryAndPreferences;

namespace RROMS.Web.Services.CustomerHistoryAndPreferences;

/// <summary>Read-only: a customer's own past reservations, orders and payments. Nothing here changes data.</summary>
public interface ICustomerHistoryService
{
    /// <summary>
    /// Everything this customer has done, newest first. Reservations are the ones that are over: cancelled,
    /// completed, or dated before today (a booking still to come stays on the Reservations page).
    /// Orders and payments are all of them.
    /// </summary>
    Task<CustomerHistory> GetAsync(int customerUserId);
}
