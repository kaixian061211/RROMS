using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.Restaurant;

namespace RROMS.Web.Pages.Menu.Manage;

[Authorize(Roles = PaymentRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public IndexModel(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    /// <summary>The category filter. It is kept in the address, so one tap applies it.</summary>
    [BindProperty(SupportsGet = true)]
    public int? CategoryId { get; set; }

    public List<MenuCategory> Categories { get; private set; } = new();

    public List<MenuItem> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Categories = await _db.Set<MenuCategory>()
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        var query = _db.Set<MenuItem>().AsNoTracking().Include(m => m.MenuCategory).AsQueryable();

        if (CategoryId is not null)
            query = query.Where(m => m.MenuCategoryId == CategoryId);

        Items = await query
            .OrderBy(m => m.MenuCategory != null ? m.MenuCategory.DisplayOrder : int.MaxValue)
            .ThenBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var item = await _db.Set<MenuItem>().FirstOrDefaultAsync(m => m.MenuItemId == id);
        if (item is null) return NotFound();

        item.IsAvailable = !item.IsAvailable;
        await _db.SaveChangesAsync();

        TempData["Message"] = item.IsAvailable ? $"{item.Name} is available again." : $"{item.Name} is now sold out.";
        return RedirectToPage(new { categoryId = CategoryId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var item = await _db.Set<MenuItem>().FirstOrDefaultAsync(m => m.MenuItemId == id);
        if (item is null) return NotFound();

        ImageStorage.Delete(item.ImagePath, _environment.WebRootPath);
        _db.Remove(item);
        await _db.SaveChangesAsync();

        TempData["Message"] = $"{item.Name} deleted.";
        return RedirectToPage(new { categoryId = CategoryId });
    }
}
