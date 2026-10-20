using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Services.OrdersAndReservations;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly IMenuItemLookup _menu;

    public OrderService(AppDbContext db, IMenuItemLookup menu)
    {
        _db = db;
        _menu = menu;
    }

    // AppDbContext picks up Order through OrderConfiguration, so it needs no DbSet for it.
    private DbSet<Order> Orders => _db.Set<Order>();

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
        var lines = order.Items
            .OrderBy(item => item.OrderItemId)
            .Select(item => new OrderLine(item, menuItems.TryGetValue(item.MenuItemId, out var menuItem)
                ? menuItem.Name
                : $"Menu item #{item.MenuItemId}"))   // a dish that has since been taken off the menu
            .ToList();

        return new OrderDetails(order, lines);
    }

    public Task<List<Order>> GetForCustomerAsync(int customerUserId) =>
        Orders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.CustomerUserId == customerUserId)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.OrderId)
            .ToListAsync();

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
}
