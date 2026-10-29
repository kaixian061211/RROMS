namespace RROMS.Web.Models.Restaurant;

public class MenuCategory
{
    public int MenuCategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Lower numbers come first on the menu.</summary>
    public int DisplayOrder { get; set; }

    public List<MenuItem> MenuItems { get; set; } = new();
}
