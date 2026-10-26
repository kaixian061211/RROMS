using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Services.Restaurant;

/// <summary>Reads the real MenuItem table for ordering. Replaces TemporaryMenuItemLookup.</summary>
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
            .Include(m => m.MenuCategory)
            .OrderBy(m => m.MenuCategory != null ? m.MenuCategory.DisplayOrder : int.MaxValue)
            .ThenBy(m => m.Name)
            .ToListAsync();

        return items.Select(ToInfo).ToList();
    }

    public async Task<IReadOnlyDictionary<int, MenuItemInfo>> GetByIdsAsync(IEnumerable<int> menuItemIds)
    {
        var ids = menuItemIds.Distinct().ToList();

        var items = await _db.Set<MenuItem>()
            .AsNoTracking()
            .Include(m => m.MenuCategory)
            .Where(m => ids.Contains(m.MenuItemId))
            .ToListAsync();

        return items.ToDictionary(item => item.MenuItemId, ToInfo);
    }

    private static MenuItemInfo ToInfo(MenuItem item) =>
        new(
            item.MenuItemId,
            item.Name,
            item.Price,
            item.IsAvailable,
            string.IsNullOrWhiteSpace(item.MenuCategory?.Name) ? "General" : item.MenuCategory.Name,
            item.MenuCategory?.DisplayOrder ?? int.MaxValue
        );
}