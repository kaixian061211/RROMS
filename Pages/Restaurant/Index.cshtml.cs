using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.Restaurant;

namespace RROMS.Web.Pages.Restaurant;

public class IndexModel : PageModel
{
    private static readonly DayOfWeek[] Week =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };

    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public RestaurantInfo? Info { get; private set; }

    /// <summary>Monday first. Empty until the restaurant has set its hours.</summary>
    public List<OperatingHour> Hours { get; private set; } = new();

    public List<Announcement> Announcements { get; private set; } = new();

    public List<Promotion> Promotions { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Info = await _db.Set<RestaurantInfo>().AsNoTracking().FirstOrDefaultAsync();

        var hours = await _db.Set<OperatingHour>().AsNoTracking().ToListAsync();
        Hours = hours.OrderBy(h => Array.IndexOf(Week, h.DayOfWeek)).ToList();

        var now = DateTime.Now;
        Announcements = await _db.Set<Announcement>()
            .AsNoTracking()
            .Where(a => a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        Promotions = await MenuPricing.LoadRunningAsync(_db);
    }
}
