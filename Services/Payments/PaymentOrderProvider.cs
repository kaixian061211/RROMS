using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Services.Payments;

public sealed class PaymentOrderProvider : IPaymentOrderProvider
{
    private readonly AppDbContext _db;

    public PaymentOrderProvider(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PaymentOrderInfo?> GetOrderAsync(int orderId)
    {
        var order = await OrdersWithItems()
            .SingleOrDefaultAsync(o => o.OrderId == orderId);

        return order is null ? null : ToPaymentOrderInfo(order);
    }

    public async Task<IReadOnlyList<PaymentOrderInfo>>
        GetOrdersForCustomerAsync(int customerUserId)
    {
        var orders = await OrdersWithItems()
            .Where(o => o.CustomerUserId == customerUserId)
            .OrderByDescending(o => o.OrderId)
            .ToListAsync();

        return orders.Select(ToPaymentOrderInfo).ToList();
    }

    public async Task<IReadOnlyDictionary<int, PaymentOrderInfo>>
        GetOrdersAsync(IEnumerable<int> orderIds)
    {
        var ids = orderIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<int, PaymentOrderInfo>();
        }

        var orders = await OrdersWithItems()
            .Where(o => ids.Contains(o.OrderId))
            .ToListAsync();

        return orders
            .Select(ToPaymentOrderInfo)
            .ToDictionary(order => order.OrderId);
    }

    public Task<IReadOnlyDictionary<int, string>> GetStaffNamesAsync(
        IEnumerable<int> staffUserIds)
    {
        // Staff records belong to the authentication module.
        return Task.FromResult<IReadOnlyDictionary<int, string>>(
            new Dictionary<int, string>());
    }

    private IQueryable<Order> OrdersWithItems() =>
        _db.Set<Order>()
            .AsNoTracking()
            .Include(order => order.Items);

    private static PaymentOrderInfo ToPaymentOrderInfo(Order order) =>
        new()
        {
            OrderId = order.OrderId,
            OrderStatus = order.Status,
            CustomerUserId = order.CustomerUserId,
            CustomerName = $"Customer #{order.CustomerUserId}",
            TotalAmount = order.Items.Sum(item => item.LineTotal)
        };
}