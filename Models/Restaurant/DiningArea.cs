namespace RROMS.Web.Models.Restaurant;

/// <summary>A section of the restaurant, such as Indoor, Outdoor or VIP room.</summary>
public class DiningArea
{
    public int DiningAreaId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<RestaurantTable> Tables { get; set; } = new();
}
