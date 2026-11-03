using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.CustomerHistoryAndPreferences;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Services.CustomerHistoryAndPreferences;

/// <summary>
/// Reads the other modules' data and shows it as history. It stores nothing of its own and never changes
/// an order, a reservation or a payment.
/// </summary>
public class CustomerHistoryService : ICustomerHistoryService
{
    private const int MaxReservations = 100;

    private readonly AppDbContext _db;
    private readonly IOrderService _orders;
    private readonly IPaymentQueries _payments;

    public CustomerHistoryService(AppDbContext db, IOrderService orders, IPaymentQueries payments)
    {
        _db = db;
        _orders = orders;
        _payments = payments;
    }

    public async Task<CustomerHistory> GetMyHistoryAsync(int customerUserId)
    {
        var reservations = await GetReservationHistoryForStaffAsync(customerUserId);

        var orders = (await _orders.GetForCustomerAsync(customerUserId))
            .Select(details => new OrderHistoryItem(
                details.Order.OrderId,
                details.Order.Status,
                details.Order.CreatedAt,
                details.Order.Total,
                string.Join(", ", details.Lines.Select(line => $"{line.Item.Quantity} x {line.MenuItemName}")),
                OrderFormatter.PaymentLabel(details.Payment),
                OrderFormatter.PaymentBadge(details.Payment)))
            .ToList();

        var payments = (await _payments.GetForCustomerAsync(customerUserId))
            .Select(payment => new PaymentHistoryItem(
                payment.PaymentId,
                payment.ReceiptNo,
                payment.OrderLabel,
                payment.Method,
                payment.Status,
                payment.Amount,
                payment.PaidAt,
                payment.HasReceipt))
            .ToList();

        return new CustomerHistory(reservations, orders, payments);
    }

    public async Task<List<ReservationHistoryItem>> GetReservationHistoryForStaffAsync(int customerUserId)
    {
        // AppDbContext picks Reservation up through its configuration class, so it needs no DbSet.
        var rows = await _db.Set<Reservation>().AsNoTracking()
            .Where(r => r.CustomerUserId == customerUserId)
            .OrderByDescending(r => r.ReservationDate)
            .ThenByDescending(r => r.TimeSlot)
            .Take(MaxReservations)
            .ToListAsync();

        return rows
            .Select(r => new ReservationHistoryItem(
                r.ReservationId, r.ReservationDate, r.TimeSlot, r.PartySize, r.Status, r.SpecialRequest))
            .ToList();
    }
}
