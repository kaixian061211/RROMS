using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Services.OrdersAndReservations;

public class OrderService : IOrderService
{
    private const string NotFoundMessage = "Order not found.";
    private const string ChangedByOthersMessage =
        "This order was changed by someone else while you were looking at it. Please reload the page and try again.";

    // The only status changes allowed: pending -> preparing -> ready -> served -> completed, and pending -> cancelled.
    private static readonly Dictionary<string, string[]> AllowedStatusChanges = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Preparing, OrderStatus.Cancelled },
        [OrderStatus.Preparing] = new[] { OrderStatus.Ready },
        [OrderStatus.Ready] = new[] { OrderStatus.Served },
        [OrderStatus.Served] = new[] { OrderStatus.Completed }
    };

    /// <summary>Orders the restaurant still has to deal with. Completed and cancelled orders are finished.</summary>
    private static readonly string[] ActiveStatuses =
        { OrderStatus.Pending, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served };

    private readonly AppDbContext _db;
    private readonly IMenuItemLookup _menu;
    private readonly IPaymentQueries _payments;

    public OrderService(AppDbContext db, IMenuItemLookup menu, IPaymentQueries payments)
    {
        _db = db;
        _menu = menu;
        _payments = payments;
    }

    // AppDbContext picks up Order through OrderConfiguration, so it needs no DbSet for it.
    private DbSet<Order> Orders => _db.Set<Order>();

    // Customer actions

    public async Task<IReadOnlyList<MenuItemInfo>> GetMenuAsync() =>
        (await _menu.GetAllAsync()).Where(menuItem => menuItem.IsAvailable).ToList();

    public async Task<OrderResult> CreateAsync(int customerUserId, OrderInput input)
    {
        // TODO: check against the real tables once the RestaurantTable table exists.
        if (input.TableId is not int tableId || !TemporaryTables.Exists(tableId))
            return OrderResult.Failure("Please choose your table.");
        if (input.Items.Any(line => line.Quantity < 0))
            return OrderResult.Failure("Quantities can't be negative.");

        // Quantity 0 means the customer didn't pick that dish. The same dish sent twice is added together.
        var lines = input.Items
            .Where(line => line.Quantity > 0)
            .GroupBy(line => line.MenuItemId)
            .Select(group => (MenuItemId: group.Key, Quantity: group.Sum(line => line.Quantity)))
            .ToList();

        if (lines.Count == 0)
            return OrderResult.Failure("Please choose at least one item.");
        if (lines.Any(line => line.Quantity > OrderLineInput.MaxQuantity))
            return OrderResult.Failure($"You can order up to {OrderLineInput.MaxQuantity} of each item.");

        var menuItems = await _menu.GetByIdsAsync(lines.Select(line => line.MenuItemId));
        var error = CheckCanOrder(lines.Select(line => line.MenuItemId), menuItems);
        if (error is not null)
            return OrderResult.Failure(error);

        var order = new Order
        {
            CustomerUserId = customerUserId,
            TableId = tableId,
            Items = lines.Select(line => new OrderItem
            {
                MenuItemId = line.MenuItemId,
                Quantity = line.Quantity,
                // Copy today's menu price, so a later price change doesn't alter this order.
                UnitPrice = menuItems[line.MenuItemId].Price
            }).ToList()
        };
        Orders.Add(order);
        await _db.SaveChangesAsync();

        return OrderResult.Success(order);
    }

    public async Task<OrderDetails?> GetByIdAsync(int customerUserId, int orderId)
    {
        var order = await Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerUserId == customerUserId);
        if (order is null)
            return null;

        var menuItems = await _menu.GetByIdsAsync(order.Items.Select(item => item.MenuItemId));
        return new OrderDetails(order, ToLines(order, menuItems));
    }

    public Task<List<Order>> GetForCustomerAsync(int customerUserId) =>
        Orders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.CustomerUserId == customerUserId)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.OrderId)
            .ToListAsync();

    // Staff actions

    public async Task<List<OrderQueueItem>> GetQueueAsync(string? status)
    {
        var query = Orders.AsNoTracking().Include(o => o.Items).AsQueryable();
        if (string.IsNullOrEmpty(status))
        {
            query = query.Where(o => ActiveStatuses.Contains(o.Status));
        }
        else
        {
            query = query.Where(o => o.Status == status);

            // Finished orders pile up, so those lists only show today's.
            if (status is OrderStatus.Completed or OrderStatus.Cancelled)
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                query = query.Where(o => o.CreatedAt >= today && o.CreatedAt < tomorrow);
            }
        }

        var orders = await query.OrderBy(o => o.CreatedAt).ThenBy(o => o.OrderId).ToListAsync();
        var menuItems = await _menu.GetByIdsAsync(orders.SelectMany(o => o.Items).Select(item => item.MenuItemId));
        var payments = await _payments.GetByOrdersAsync(orders.Select(o => o.OrderId));

        return orders.Select(order =>
        {
            payments.TryGetValue(order.OrderId, out var payment);
            return new OrderQueueItem(order, ToLines(order, menuItems), payment, GetAllowedStatusChanges(order, payment));
        }).ToList();
    }

    public async Task<OrderResult> ChangeStatusAsync(int orderId, string newStatus, byte[] rowVersion)
    {
        var order = await Orders.FindAsync(orderId);
        if (order is null)
            return OrderResult.Failure(NotFoundMessage);

        var payment = await _payments.GetByOrderAsync(orderId);
        var error = CheckStatusChange(order, newStatus, payment);
        if (error is not null)
            return OrderResult.Failure(error);

        order.Status = newStatus;
        // EF only updates the row if it still has the RowVersion the staff member's page loaded,
        // so a change saved by someone else in between is caught instead of overwritten.
        _db.Entry(order).Property(o => o.RowVersion).OriginalValue = rowVersion;

        try
        {
            await _db.SaveChangesAsync();
            return OrderResult.Success(order);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OrderResult.Failure(ChangedByOthersMessage);
        }
    }

    // Rules

    /// <summary>The statuses this order may move to right now, so the queue only offers those buttons.</summary>
    private static IReadOnlyList<string> GetAllowedStatusChanges(Order order, PaymentDetails? payment) =>
        OrderStatus.All.Where(status => CheckStatusChange(order, status, payment) is null).ToList();

    private static string? CheckStatusChange(Order order, string newStatus, PaymentDetails? payment)
    {
        if (!AllowedStatusChanges.TryGetValue(order.Status, out var allowed) || !allowed.Contains(newStatus))
            return $"A {order.Status} order can't be changed to {newStatus}.";
        // A customer can pay online while the order is still pending. Cancelling it then would keep their money.
        if (newStatus == OrderStatus.Cancelled && payment?.Status == PaymentStatus.Paid)
            return "This order has been paid, so it can't be cancelled. Refund the payment first.";
        // TODO: agree with the payment module whether an order must be paid before it can be marked completed.
        return null;
    }

    private static string? CheckCanOrder(IEnumerable<int> menuItemIds, IReadOnlyDictionary<int, MenuItemInfo> menuItems)
    {
        foreach (var menuItemId in menuItemIds)
        {
            if (!menuItems.TryGetValue(menuItemId, out var menuItem))
                return "One of the items you chose is no longer on the menu. Please check your order.";
            if (!menuItem.IsAvailable)
                return $"Sorry, {menuItem.Name} isn't available right now.";
        }
        return null;
    }

    /// <summary>The order's items with their menu names, in the order they were added.</summary>
    private static List<OrderLine> ToLines(Order order, IReadOnlyDictionary<int, MenuItemInfo> menuItems) =>
        order.Items
            .OrderBy(item => item.OrderItemId)
            .Select(item => new OrderLine(item, menuItems.TryGetValue(item.MenuItemId, out var menuItem)
                ? menuItem.Name
                : $"Menu item #{item.MenuItemId}"))   // a dish that has since been taken off the menu
            .ToList();
}
