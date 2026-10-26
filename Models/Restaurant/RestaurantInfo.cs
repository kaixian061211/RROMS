namespace RROMS.Web.Models.Restaurant;

/// <summary>The restaurant's own details and settings. Only one row is used.</summary>
public class RestaurantInfo
{
    public int RestaurantInfoId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? LogoPath { get; set; }

    public decimal ServiceChargePercent { get; set; }

    public decimal TaxPercent { get; set; }

    public bool IsAcceptingOrders { get; set; } = true;

    public bool IsAcceptingReservations { get; set; } = true;
}
