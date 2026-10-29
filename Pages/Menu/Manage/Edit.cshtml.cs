using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.Restaurant;

namespace RROMS.Web.Pages.Menu.Manage;

/// <summary>Adds a menu item, or edits one when ?id= is given.</summary>
[Authorize(Roles = PaymentRoles.Admin)]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public EditModel(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? ImageFile { get; set; }

    public SelectList CategoryOptions { get; private set; } = new(Array.Empty<MenuCategory>(), "MenuCategoryId", "Name");

    /// <summary>The picture already saved, shown above the upload box.</summary>
    public string? ImagePath { get; private set; }

    public class InputModel
    {
        public int MenuItemId { get; set; }

        [Required(ErrorMessage = "Enter the name of the dish.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter a short description.")]
        [StringLength(500, ErrorMessage = "Keep the description to 500 characters or fewer.")]
        public string Description { get; set; } = string.Empty;

        [Range(0.01, 99999999.99, ErrorMessage = "Enter a price above RM 0.00.")]
        public decimal Price { get; set; }

        public int? MenuCategoryId { get; set; }

        public bool IsAvailable { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadOptionsAsync();

        if (id is not null)
        {
            var item = await _db.Set<MenuItem>().AsNoTracking().FirstOrDefaultAsync(m => m.MenuItemId == id);
            if (item is null) return NotFound();

            ImagePath = item.ImagePath;
            Input = new InputModel
            {
                MenuItemId = item.MenuItemId,
                Name = item.Name,
                Description = item.Description,
                Price = item.Price,
                MenuCategoryId = item.MenuCategoryId,
                IsAvailable = item.IsAvailable
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ImageFile is not null && ImageStorage.Validate(ImageFile) is { } problem)
            ModelState.AddModelError(nameof(ImageFile), problem);

        if (Input.MenuCategoryId is not null && !await _db.Set<MenuCategory>().AnyAsync(c => c.MenuCategoryId == Input.MenuCategoryId))
            ModelState.AddModelError("Input.MenuCategoryId", "Choose a category from the list.");

        MenuItem? item = null;
        if (Input.MenuItemId != 0)
        {
            item = await _db.Set<MenuItem>().FirstOrDefaultAsync(m => m.MenuItemId == Input.MenuItemId);
            if (item is null) return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ImagePath = item?.ImagePath;
            await LoadOptionsAsync();
            return Page();
        }

        if (item is null)
        {
            item = new MenuItem();
            _db.Add(item);
        }

        item.Name = Input.Name;
        item.Description = Input.Description;
        item.Price = Input.Price;
        item.MenuCategoryId = Input.MenuCategoryId;
        item.IsAvailable = Input.IsAvailable;

        if (ImageFile is not null)
        {
            ImageStorage.Delete(item.ImagePath, _environment.WebRootPath);
            item.ImagePath = await ImageStorage.SaveAsync(ImageFile, _environment.WebRootPath, "menu");
        }

        await _db.SaveChangesAsync();

        TempData["Message"] = Input.MenuItemId == 0 ? $"{item.Name} added to the menu." : $"{item.Name} saved.";
        return RedirectToPage("/Menu/Manage/Index");
    }

    private async Task LoadOptionsAsync()
    {
        var categories = await _db.Set<MenuCategory>()
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        CategoryOptions = new SelectList(categories, nameof(MenuCategory.MenuCategoryId), nameof(MenuCategory.Name));
    }
}
