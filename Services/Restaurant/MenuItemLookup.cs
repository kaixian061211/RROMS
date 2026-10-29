using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Services.Restaurant;

/// <summary>
/// Reads the real MenuItem table for ordering. Replaces TemporaryMenuItemLookup.
/// The price it returns is the price after any running promotion, so an order is charged what the
/// customer saw on the menu. To charge the normal price instead, use item.Price in ToInfo.
/// </summary>
public class MenuItemLookup : IMenuItemLookup
{
    private readonly AppDbContext _db;

    public MenuItemLookup(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MenuItemInfo>> GetAllAsync()
    {
        var items = await _db.Set<MenuItem>()
            .AsNoTracking()
            .OrderBy(m => m.MenuCategory != null ? m.MenuCategory.DisplayOrder : int.MaxValue)
            .ThenBy(m => m.Name)
            .ToListAsync();

        var promotions = await MenuPricing.LoadRunningAsync(_db);

        return items.Select(item => ToInfo(item, promotions)).ToList();
    }

    public async Task<IReadOnlyDictionary<int, MenuItemInfo>> GetByIdsAsync(IEnumerable<int> menuItemIds)
    {
        var ids = menuItemIds.Distinct().ToList();

        var items = await _db.Set<MenuItem>()
            .AsNoTracking()
            .Where(m => ids.Contains(m.MenuItemId))
            .ToListAsync();

        var promotions = await MenuPricing.LoadRunningAsync(_db);

        return items.ToDictionary(item => item.MenuItemId, item => ToInfo(item, promotions));
    }

    private static MenuItemInfo ToInfo(MenuItem item, IEnumerable<Promotion> promotions) =>
        new(
            item.MenuItemId,
            item.Name,
            MenuPricing.BestPrice(item, promotions).Price,
            item.IsAvailable,
            item.MenuCategory?.Name ?? "Uncategorized"
        );
}