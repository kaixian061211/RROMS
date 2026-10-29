using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Restaurant;

namespace RROMS.Web.Pages.Menu.Manage;

[Authorize(Roles = PaymentRoles.Admin)]
public class CategoriesModel : PageModel
{
    private readonly AppDbContext _db;

    public CategoriesModel(AppDbContext db)
    {
        _db = db;
    }

    public List<CategoryRow> Categories { get; private set; } = new();

    public record CategoryRow(int MenuCategoryId, string Name, string? Description, int DisplayOrder, int ItemCount);

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int MenuCategoryId { get; set; }

        [Required(ErrorMessage = "Enter a name for the category.")]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Keep the description to 200 characters or fewer.")]
        public string? Description { get; set; }

        [Range(0, 999, ErrorMessage = "Enter a position between 0 and 999.")]
        public int DisplayOrder { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadAsync();

        if (id is not null)
        {
            var category = await _db.Set<MenuCategory>().AsNoTracking().FirstOrDefaultAsync(c => c.MenuCategoryId == id);
            if (category is null) return NotFound();

            Input = new InputModel
            {
                MenuCategoryId = category.MenuCategoryId,
                Name = category.Name,
                Description = category.Description,
                DisplayOrder = category.DisplayOrder
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (await _db.Set<MenuCategory>().AnyAsync(c => c.Name == Input.Name && c.MenuCategoryId != Input.MenuCategoryId))
            ModelState.AddModelError("Input.Name", "A category with this name already exists. Choose another name.");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        if (Input.MenuCategoryId == 0)
        {
            _db.Add(new MenuCategory { Name = Input.Name, Description = Input.Description, DisplayOrder = Input.DisplayOrder });
            TempData["Message"] = $"{Input.Name} added.";
        }
        else
        {
            var category = await _db.Set<MenuCategory>().FirstOrDefaultAsync(c => c.MenuCategoryId == Input.MenuCategoryId);
            if (category is null) return NotFound();

            category.Name = Input.Name;
            category.Description = Input.Description;
            category.DisplayOrder = Input.DisplayOrder;
            TempData["Message"] = $"{Input.Name} saved.";
        }

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var category = await _db.Set<MenuCategory>().FirstOrDefaultAsync(c => c.MenuCategoryId == id);
        if (category is null) return NotFound();

        // its dishes stay on the menu, without a category
        _db.Remove(category);
        await _db.SaveChangesAsync();

        TempData["Message"] = $"{category.Name} deleted. Its dishes are still on the menu.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Categories = await _db.Set<MenuCategory>()
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryRow(c.MenuCategoryId, c.Name, c.Description, c.DisplayOrder, c.MenuItems.Count))
            .ToListAsync();
    }
}
