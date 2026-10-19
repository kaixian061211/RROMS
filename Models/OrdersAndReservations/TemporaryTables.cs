namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>
/// Stand-in for the RestaurantTable table, which belongs to another module and doesn't exist yet.
/// TODO: replace with the real tables, and add the capacity check, once RestaurantTable is on main.
/// </summary>
public static class TemporaryTables
{
    public static readonly int[] TableIds = Enumerable.Range(1, 10).ToArray();

    public static bool Exists(int tableId) => TableIds.Contains(tableId);
}
