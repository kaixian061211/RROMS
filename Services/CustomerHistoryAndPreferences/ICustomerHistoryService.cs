using RROMS.Web.Models.CustomerHistoryAndPreferences;

namespace RROMS.Web.Services.CustomerHistoryAndPreferences;

public interface ICustomerHistoryService
{
    /// <summary>
    /// The signed-in customer's own reservations, orders and payments, newest first. The page passes the
    /// customer's id from the sign-in, so a customer can only ever read their own history.
    /// </summary>
    Task<CustomerHistory> GetMyHistoryAsync(int customerUserId);

    /// <summary>
    /// One customer's reservations for staff, newest first. Reservations only: orders and payments
    /// are not part of the staff view of a customer.
    /// </summary>
    Task<List<ReservationHistoryItem>> GetReservationHistoryForStaffAsync(int customerUserId);
}
