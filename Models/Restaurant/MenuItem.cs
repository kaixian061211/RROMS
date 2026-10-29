namespace RROMS.Web.Models.Restaurant;

/// <summary>A dish or drink on the menu. Ordering reads it through IMenuItemLookup.</summary>
public class MenuItem
{
    public int MenuItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>The normal price, before any running promotion.</summary>
    public decimal Price { get; set; }

    /// <summary>Web path of the uploaded picture, for example /images/menu/abc.jpg.</summary>
    public string? ImagePath { get; set; }

    public int? MenuCategoryId { get; set; }
    public MenuCategory? MenuCategory { get; set; }

    /// <summary>False shows the dish as sold out and stops it being ordered.</summary>
    public bool IsAvailable { get; set; } = true;

    public List<Promotion> Promotions { get; set; } = new();
}
