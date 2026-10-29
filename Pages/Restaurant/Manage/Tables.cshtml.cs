using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Restaurant;

namespace RROMS.Web.Pages.Restaurant.Manage;

[Authorize(Roles = PaymentRoles.Admin)]
public class TablesModel : PageModel
{
    private readonly AppDbContext _db;

    public TablesModel(AppDbContext db)
    {
        _db = db;
    }

    public List<RestaurantTable> Tables { get; private set; } = new();

    public SelectList AreaOptions { get; private set; } = new(Array.Empty<DiningArea>(), "DiningAreaId", "Name");

    public bool HasAreas { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int RestaurantTableId { get; set; }

        [Required(ErrorMessage = "Enter the table number.")]
        [StringLength(20)]
        public string TableNumber { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Choose a dining area.")]
        public int DiningAreaId { get; set; }

        [Range(1, 20, ErrorMessage = "Seats must be between 1 and 20.")]
        public int Capacity { get; set; } = 2;

        public bool IsActive { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadAsync();

        if (id is not null)
        {
            var table = await _db.Set<RestaurantTable>().AsNoTracking().FirstOrDefaultAsync(t => t.RestaurantTableId == id);
            if (table is null) return NotFound();

            Input = new InputModel
            {
                RestaurantTableId = table.RestaurantTableId,
                TableNumber = table.TableNumber,
                DiningAreaId = table.DiningAreaId,
                Capacity = table.Capacity,
                IsActive = table.IsActive
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (await _db.Set<RestaurantTable>().AnyAsync(t => t.TableNumber == Input.TableNumber && t.RestaurantTableId != Input.RestaurantTableId))
            ModelState.AddModelError("Input.TableNumber", "That table number is already used. Choose another number.");

        if (Input.DiningAreaId != 0 && !await _db.Set<DiningArea>().AnyAsync(a => a.DiningAreaId == Input.DiningAreaId))
            ModelState.AddModelError("Input.DiningAreaId", "Choose a dining area from the list.");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        if (Input.RestaurantTableId == 0)
        {
            _db.Add(new RestaurantTable
            {
                TableNumber = Input.TableNumber,
                DiningAreaId = Input.DiningAreaId,
                Capacity = Input.Capacity,
                IsActive = Input.IsActive
            });
            TempData["Message"] = $"Table {Input.TableNumber} added.";
        }
        else
        {
            var table = await _db.Set<RestaurantTable>().FirstOrDefaultAsync(t => t.RestaurantTableId == Input.RestaurantTableId);
            if (table is null) return NotFound();

            table.TableNumber = Input.TableNumber;
            table.DiningAreaId = Input.DiningAreaId;
            table.Capacity = Input.Capacity;
            table.IsActive = Input.IsActive;
            TempData["Message"] = $"Table {Input.TableNumber} saved.";
        }

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var areas = await _db.Set<DiningArea>().AsNoTracking().OrderBy(a => a.Name).ToListAsync();
        HasAreas = areas.Count > 0;
        AreaOptions = new SelectList(areas, nameof(DiningArea.DiningAreaId), nameof(DiningArea.Name));

        Tables = await _db.Set<RestaurantTable>()
            .AsNoTracking()
            .Include(t => t.DiningArea)
            .OrderBy(t => t.DiningArea.Name)
            .ThenBy(t => t.TableNumber.Length)
            .ThenBy(t => t.TableNumber)
            .ToListAsync();
    }
}
