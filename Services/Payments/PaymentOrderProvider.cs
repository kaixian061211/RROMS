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

        if (order is null)
        {
            return null;
        }

        var customer = await GetCustomerAsync(order.CustomerUserId);
        return ToPaymentOrderInfo(order, customer);
    }

    public async Task<IReadOnlyList<PaymentOrderInfo>>
        GetOrdersForCustomerAsync(int customerUserId)
    {
        var orders = await OrdersWithItems()
            .Where(o => o.CustomerUserId == customerUserId)
            .OrderByDescending(o => o.OrderId)
            .ToListAsync();

        var customer = await GetCustomerAsync(customerUserId);
        return orders.Select(order => ToPaymentOrderInfo(order, customer)).ToList();
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

        var userIds = orders
            .Select(o => o.CustomerUserId)
            .Distinct()
            .ToList();

        var customers = await GetCustomersAsync(userIds);

        return orders
            .Select(order => ToPaymentOrderInfo(order, customers))
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

    private async Task<UserAccount?> GetCustomerAsync(int customerUserId)
    {
        if (customerUserId <= 0)
        {
            return null;
        }

        return await _db.Set<UserAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserId == customerUserId);
    }

    private async Task<IReadOnlyDictionary<int, UserAccount>> GetCustomersAsync(
        IEnumerable<int> customerUserIds)
    {
        var userIds = customerUserIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
        {
            return new Dictionary<int, UserAccount>();
        }

        var accounts = await _db.Set<UserAccount>()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .ToListAsync();

        return accounts.ToDictionary(u => u.UserId);
    }

    private static PaymentOrderInfo ToPaymentOrderInfo(
        Order order,
        UserAccount? customer)
    {
        var customerName = customer is { FullName: not null and not "" }
            ? customer.FullName.Trim()
            : $"Customer #{order.CustomerUserId}";

        return new PaymentOrderInfo
        {
            OrderId = order.OrderId,
            OrderStatus = order.Status,
            CustomerUserId = order.CustomerUserId,
            CustomerName = customerName,
            CustomerEmail = customer?.Email ?? string.Empty,
            TotalAmount = order.Items.Sum(item => item.LineTotal)
        };
    }

    private static PaymentOrderInfo ToPaymentOrderInfo(
        Order order,
        IReadOnlyDictionary<int, UserAccount> customers)
    {
        customers.TryGetValue(order.CustomerUserId, out var customer);
        return ToPaymentOrderInfo(order, customer);
    }
}