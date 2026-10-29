using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Accounts;
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

        return order is null ? null : (await MapOrdersAsync(new[] { order })).Single();
    }

    public async Task<IReadOnlyList<PaymentOrderInfo>>
        GetOrdersForCustomerAsync(int customerUserId)
    {
        var orders = await OrdersWithItems()
            .Where(o => o.CustomerUserId == customerUserId)
            .OrderByDescending(o => o.OrderId)
            .ToListAsync();

        return await MapOrdersAsync(orders);
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

        return (await MapOrdersAsync(orders))
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

    private async Task<List<PaymentOrderInfo>> MapOrdersAsync(IReadOnlyCollection<Order> orders)
    {
        var customerIds = orders.Select(o => o.CustomerUserId).Distinct().ToList();
        var customers = await _db.Set<UserAccount>().AsNoTracking()
            .Where(u => customerIds.Contains(u.UserId) && u.Role == UserRoles.Customer)
            .Select(u => new { u.UserId, u.FullName, u.Email, u.IsActive })
            .ToDictionaryAsync(u => u.UserId);

        return orders.Select(order =>
        {
            var result = ToPaymentOrderInfo(order);
            if (customers.TryGetValue(order.CustomerUserId, out var customer))
            {
                result.CustomerName = customer.FullName;
                result.CustomerEmail = customer.IsActive ? customer.Email : string.Empty;
            }
            return result;
        }).ToList();
    }

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
