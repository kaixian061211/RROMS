using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Restaurant;

namespace RROMS.Web.Services.OrdersAndReservations;

/// <summary>Reads the restaurant module's tables (RestaurantTable, OperatingHour, RestaurantInfo). Read-only.</summary>
public class RestaurantLookup : IRestaurantLookup
{
    private readonly AppDbContext _db;

    public RestaurantLookup(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<int, TableInfo>> GetTablesAsync() =>
        await _db.Set<RestaurantTable>().AsNoTracking()
            .Select(t => new TableInfo(t.RestaurantTableId, t.TableNumber, t.DiningArea.Name, t.Capacity, t.IsActive))
            .ToDictionaryAsync(t => t.TableId);

    public async Task<OpeningHours?> GetOpeningHoursAsync(DayOfWeek day) =>
        await _db.Set<OperatingHour>().AsNoTracking()
            .Where(h => h.DayOfWeek == day)
            .Select(h => new OpeningHours(h.IsClosed, h.OpenTime, h.CloseTime))
            .FirstOrDefaultAsync();

    // Only one RestaurantInfo row is used. With none yet, nothing has been switched off.
    public async Task<bool> IsAcceptingReservationsAsync() =>
        await _db.Set<RestaurantInfo>().AsNoTracking().Select(r => (bool?)r.IsAcceptingReservations).FirstOrDefaultAsync() ?? true;

    public async Task<bool> IsAcceptingOrdersAsync() =>
        await _db.Set<RestaurantInfo>().AsNoTracking().Select(r => (bool?)r.IsAcceptingOrders).FirstOrDefaultAsync() ?? true;
}
