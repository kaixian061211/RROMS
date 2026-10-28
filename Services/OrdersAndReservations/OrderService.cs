using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;
using RROMS.Web.Services.Notifications;

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
    private readonly IRestaurantLookup _restaurant;
    private readonly INotificationService _notifications;
    private readonly INotificationDelivery _notificationDelivery;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        AppDbContext db,
        IMenuItemLookup menu,
        IPaymentQueries payments,
        IRestaurantLookup restaurant,
        INotificationService notifications,
        INotificationDelivery notificationDelivery,
        ILogger<OrderService> logger)
    {
        _db = db;
        _menu = menu;
        _payments = payments;
        _restaurant = restaurant;
        _notifications = notifications;
        _notificationDelivery = notificationDelivery;
        _logger = logger;
    }

    // AppDbContext picks up Order through OrderConfiguration, so it needs no DbSet for it.
    private DbSet<Order> Orders => _db.Set<Order>();

    // Customer actions

    public async Task<IReadOnlyList<MenuItemInfo>> GetMenuAsync() =>
        (await _menu.GetAllAsync()).Where(menuItem => menuItem.IsAvailable).ToList();

    public Task<IReadOnlyDictionary<int, TableInfo>> GetTablesAsync() => _restaurant.GetTablesAsync();

    public async Task<OrderResult> CreateAsync(int customerUserId, OrderInput input)
    {
        if (!await _restaurant.IsAcceptingOrdersAsync())
            return OrderResult.Failure("Sorry, we're not taking orders right now. Please ask a member of staff.");
        // A retired table can't take new orders.
        var tables = await _restaurant.GetTablesAsync();
        if (input.TableId is not int tableId || !tables.TryGetValue(tableId, out var table) || !table.IsActive)
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
        await NotifyCustomerAsync(
            order,
            "Order placed",
            $"Your order #{order.OrderId} has been placed.");

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
        var payment = await _payments.GetByOrderAsync(order.OrderId);
        return new OrderDetails(order, ToLines(order, menuItems), payment, CanPay(order, payment));
    }

    public async Task<List<OrderDetails>> GetForCustomerAsync(int customerUserId)
    {
        var orders = await Orders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.CustomerUserId == customerUserId)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.OrderId)
            .ToListAsync();
        var menuItems = await _menu.GetByIdsAsync(orders.SelectMany(o => o.Items).Select(item => item.MenuItemId));
        var payments = await _payments.GetByOrdersAsync(orders.Select(o => o.OrderId));

        return orders.Select(order =>
        {
            payments.TryGetValue(order.OrderId, out var payment);
            return new OrderDetails(order, ToLines(order, menuItems), payment, CanPay(order, payment));
        }).ToList();
    }

    /// <summary>
    /// A customer can go to payment unless the order is cancelled or its payment is already paid or refunded.
    /// A pending payment can be continued and a failed one tried again.
    /// </summary>
    private static bool CanPay(Order order, PaymentDetails? payment) =>
        order.Status != OrderStatus.Cancelled
        && payment?.Status is not (PaymentStatus.Paid or PaymentStatus.Refunded);

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
            await NotifyCustomerAsync(
                order,
                "Order status updated",
                $"Your order #{order.OrderId} is now {OrderFormatter.StatusLabel(newStatus).ToLowerInvariant()}.");
            return OrderResult.Success(order);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OrderResult.Failure(ChangedByOthersMessage);
        }
    }

    private async Task NotifyCustomerAsync(Order order, string title, string message)
    {
        try
        {
            var notification = await _notifications.StageAsync(new NotificationInput
            {
                UserId = order.CustomerUserId,
                Type = NotificationTypes.Order,
                Title = title,
                Message = message,
                OrderId = order.OrderId
            });

            await _db.SaveChangesAsync();
            await _notificationDelivery.DeliverAsync(notification.NotificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Order {OrderId} was saved, but its notification could not be staged or delivered.",
                order.OrderId);
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
        // The same while a payment is still going: Stripe takes the money even if the order was cancelled in the meantime.
        // An online payment ends by itself when its checkout runs out; staff can void a counter payment.
        if (newStatus == OrderStatus.Cancelled && payment?.Status == PaymentStatus.Pending)
            return payment.IsAwaitingCounter
                ? "This order is waiting to be paid at the counter. Cancel that payment under Counter payments first, then cancel the order."
                : "The customer is paying for this order online. Wait until the payment finishes or runs out, then try again.";
        // An order is only finished once it's been paid. A refunded payment was paid too, so its order can still be closed.
        if (newStatus == OrderStatus.Completed && payment?.Status is not (PaymentStatus.Paid or PaymentStatus.Refunded))
            return "This order hasn't been paid yet. Take the payment before marking it as completed.";
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
