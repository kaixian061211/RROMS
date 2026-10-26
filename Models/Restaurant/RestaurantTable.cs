namespace RROMS.Web.Models.Restaurant;

/// <summary>A table customers can book. Replaces TemporaryTables once reservations switch over.</summary>
public class RestaurantTable
{
    public int RestaurantTableId { get; set; }

    /// <summary>The number shown to people, for example "4" or "A2".</summary>
    public string TableNumber { get; set; } = string.Empty;

    public int DiningAreaId { get; set; }
    public DiningArea DiningArea { get; set; } = null!;

    public int Capacity { get; set; }

    /// <summary>False retires the table, so old bookings keep their table but no new ones can use it.</summary>
    public bool IsActive { get; set; } = true;
}
