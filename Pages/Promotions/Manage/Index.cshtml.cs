using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.Restaurant;

namespace RROMS.Web.Pages.Promotions.Manage;

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

    public DateOnly Today { get; } = DateOnly.FromDateTime(DateTime.Today);

    public List<Promotion> Running { get; private set; } = new();

    public List<Promotion> Scheduled { get; private set; } = new();

    /// <summary>Expired or switched off.</summary>
    public List<Promotion> Past { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var promotions = await _db.Set<Promotion>()
            .AsNoTracking()
            .Include(p => p.MenuItems)
            .OrderBy(p => p.StartDate)
            .ToListAsync();

        Running = promotions.Where(p => p.IsRunningOn(Today)).ToList();
        Scheduled = promotions.Where(p => p.IsActive && p.StartDate > Today).ToList();
        Past = promotions.Except(Running).Except(Scheduled).OrderByDescending(p => p.EndDate).ToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var promotion = await _db.Set<Promotion>().FirstOrDefaultAsync(p => p.PromotionId == id);
        if (promotion is null) return NotFound();

        ImageStorage.Delete(promotion.BannerPath, _environment.WebRootPath);
        _db.Remove(promotion);
        await _db.SaveChangesAsync();

        TempData["Message"] = $"{promotion.Title} deleted.";
        return RedirectToPage();
    }
}
