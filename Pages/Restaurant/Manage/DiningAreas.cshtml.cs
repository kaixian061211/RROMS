using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Restaurant;

namespace RROMS.Web.Pages.Restaurant.Manage;

[Authorize(Roles = PaymentRoles.Admin)]
public class DiningAreasModel : PageModel
{
    private readonly AppDbContext _db;

    public DiningAreasModel(AppDbContext db)
    {
        _db = db;
    }

    public List<AreaRow> Areas { get; private set; } = new();

    public record AreaRow(int DiningAreaId, string Name, string? Description, int TableCount);

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int DiningAreaId { get; set; }

        [Required(ErrorMessage = "Enter a name for the dining area.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(250, ErrorMessage = "Keep the description to 250 characters or fewer.")]
        public string? Description { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadAsync();

        if (id is not null)
        {
            var area = await _db.Set<DiningArea>().AsNoTracking().FirstOrDefaultAsync(a => a.DiningAreaId == id);
            if (area is null) return NotFound();

            Input = new InputModel { DiningAreaId = area.DiningAreaId, Name = area.Name, Description = area.Description };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (await _db.Set<DiningArea>().AnyAsync(a => a.Name == Input.Name && a.DiningAreaId != Input.DiningAreaId))
            ModelState.AddModelError("Input.Name", "A dining area with this name already exists. Choose another name.");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        if (Input.DiningAreaId == 0)
        {
            _db.Add(new DiningArea { Name = Input.Name, Description = Input.Description });
            TempData["Message"] = $"{Input.Name} added.";
        }
        else
        {
            var area = await _db.Set<DiningArea>().FirstOrDefaultAsync(a => a.DiningAreaId == Input.DiningAreaId);
            if (area is null) return NotFound();

            area.Name = Input.Name;
            area.Description = Input.Description;
            TempData["Message"] = $"{Input.Name} saved.";
        }

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var area = await _db.Set<DiningArea>().FirstOrDefaultAsync(a => a.DiningAreaId == id);
        if (area is null) return NotFound();

        if (await _db.Set<RestaurantTable>().AnyAsync(t => t.DiningAreaId == id))
        {
            TempData["Error"] = $"{area.Name} still has tables. Move them to another dining area first.";
            return RedirectToPage();
        }

        _db.Remove(area);
        await _db.SaveChangesAsync();

        TempData["Message"] = $"{area.Name} deleted.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Areas = await _db.Set<DiningArea>()
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new AreaRow(a.DiningAreaId, a.Name, a.Description, a.Tables.Count))
            .ToListAsync();
    }
}
