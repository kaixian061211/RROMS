using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.CustomerHistoryAndPreferences;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Services.CustomerHistoryAndPreferences;

public class CustomerHistoryService : ICustomerHistoryService
{
    private readonly AppDbContext _db;
    private readonly IOrderService _orders;
    private readonly IPaymentQueries _payments;
    private readonly IRestaurantLookup _restaurant;

    public CustomerHistoryService(
        AppDbContext db,
        IOrderService orders,
        IPaymentQueries payments,
        IRestaurantLookup restaurant)
    {
        _db = db;
        _orders = orders;
        _payments = payments;
        _restaurant = restaurant;
    }

    public async Task<CustomerHistory> GetAsync(int customerUserId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // One after another: they all share this request's DbContext.
        var reservations = await _db.Set<Reservation>()
            .AsNoTracking()
            .Where(r => r.CustomerUserId == customerUserId
                        && (r.ReservationDate < today
                            || r.Status == ReservationStatus.Cancelled
                            || r.Status == ReservationStatus.Completed))
            .OrderByDescending(r => r.ReservationDate)
            .ThenByDescending(r => r.TimeSlot)
            .ThenByDescending(r => r.ReservationId)
            .ToListAsync();

        var orders = await _orders.GetForCustomerAsync(customerUserId);
        var payments = await _payments.GetForCustomerAsync(customerUserId);
        var tables = await _restaurant.GetTablesAsync();

        return new CustomerHistory(reservations, orders, payments, tables);
    }
}
