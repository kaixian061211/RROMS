using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Restaurant;

namespace RROMS.Web.Pages.Restaurant.Manage;

[Authorize(Roles = PaymentRoles.Admin)]
public class HoursModel : PageModel
{
    private static readonly DayOfWeek[] Week =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };

    private readonly AppDbContext _db;

    public HoursModel(AppDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public List<HourInput> Hours { get; set; } = new();

    public class HourInput
    {
        public DayOfWeek Day { get; set; }
        public bool IsClosed { get; set; }
        public TimeOnly? OpenTime { get; set; }
        public TimeOnly? CloseTime { get; set; }
    }

    public async Task OnGetAsync()
    {
        var saved = await _db.Set<OperatingHour>().AsNoTracking().ToListAsync();

        // a day with no saved hours yet starts empty, so nothing is made up
        Hours = Week.Select(day =>
        {
            var row = saved.FirstOrDefault(h => h.DayOfWeek == day);
            return row is null
                ? new HourInput { Day = day }
                : new HourInput { Day = day, IsClosed = row.IsClosed, OpenTime = row.OpenTime, CloseTime = row.CloseTime };
        }).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Hours.Count != Week.Length || Hours.Select(h => h.Day).Distinct().Count() != Week.Length)
            return BadRequest();

        for (var i = 0; i < Hours.Count; i++)
        {
            var hour = Hours[i];
            if (hour.IsClosed) continue;

            if (hour.OpenTime is null || hour.CloseTime is null)
                ModelState.AddModelError($"Hours[{i}].OpenTime", "Enter the opening and closing time, or tick Closed.");
            else if (hour.CloseTime <= hour.OpenTime)
                ModelState.AddModelError($"Hours[{i}].CloseTime", "Closing time must be after opening time.");
        }

        if (!ModelState.IsValid) return Page();

        var saved = await _db.Set<OperatingHour>().ToListAsync();

        foreach (var hour in Hours)
        {
            var row = saved.FirstOrDefault(h => h.DayOfWeek == hour.Day);
            if (row is null)
            {
                row = new OperatingHour { DayOfWeek = hour.Day };
                _db.Add(row);
            }

            row.IsClosed = hour.IsClosed;
            row.OpenTime = hour.IsClosed ? null : hour.OpenTime;
            row.CloseTime = hour.IsClosed ? null : hour.CloseTime;
        }

        await _db.SaveChangesAsync();

        TempData["Message"] = "Opening hours saved.";
        return RedirectToPage();
    }
}
