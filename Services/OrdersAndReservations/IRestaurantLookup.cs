using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Services.OrdersAndReservations;

/// <summary>What reservations and orders read from the restaurant module: its tables, opening hours and settings.</summary>
public interface IRestaurantLookup
{
    /// <summary>Every table, retired ones too (so old bookings still show their table), by id.</summary>
    Task<IReadOnlyDictionary<int, TableInfo>> GetTablesAsync();

    /// <summary>The hours for one day of the week, or null when none have been set (then any seating is allowed).</summary>
    Task<OpeningHours?> GetOpeningHoursAsync(DayOfWeek day);

    /// <summary>False when the restaurant has switched off new reservations. True when nothing has been set.</summary>
    Task<bool> IsAcceptingReservationsAsync();

    /// <summary>False when the restaurant has switched off new orders. True when nothing has been set.</summary>
    Task<bool> IsAcceptingOrdersAsync();
}
