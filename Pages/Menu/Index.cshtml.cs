using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.Restaurant;

namespace RROMS.Web.Pages.Menu;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>The category filter. It is kept in the address, so one tap applies it.</summary>
    [BindProperty(SupportsGet = true)]
    public int? CategoryId { get; set; }

    public List<MenuCategory> Categories { get; private set; } = new();

    public List<MenuGroup> Groups { get; private set; } = new();

    public List<Promotion> Promotions { get; private set; } = new();

    public record MenuGroup(string Title, List<MenuItem> Items);

    public async Task OnGetAsync()
    {
        Categories = await _db.Set<MenuCategory>()
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        Promotions = await MenuPricing.LoadRunningAsync(_db);

        var query = _db.Set<MenuItem>().AsNoTracking().AsQueryable();
        if (CategoryId is not null)
            query = query.Where(m => m.MenuCategoryId == CategoryId);

        var items = await query.OrderBy(m => m.Name).ToListAsync();

        Groups = Categories
            .Select(c => new MenuGroup(c.Name, items.Where(m => m.MenuCategoryId == c.MenuCategoryId).ToList()))
            .Where(g => g.Items.Count > 0)
            .ToList();

        var uncategorised = items.Where(m => m.MenuCategoryId is null).ToList();
        if (uncategorised.Count > 0)
            Groups.Add(new MenuGroup("Other dishes", uncategorised));
    }

    public (decimal Price, Promotion? Promotion) PriceOf(MenuItem item) => MenuPricing.BestPrice(item, Promotions);
}
