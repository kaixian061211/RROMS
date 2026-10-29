using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.Notifications;

namespace RROMS.Web.Pages.Restaurant.Manage;

[Authorize(Roles = PaymentRoles.Admin)]
public class AnnouncementsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly NotificationPublisher _notifications;

    public AnnouncementsModel(AppDbContext db, NotificationPublisher notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public List<Announcement> Announcements { get; private set; } = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int AnnouncementId { get; set; }

        [Required(ErrorMessage = "Enter a title.")]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the message customers should see.")]
        [StringLength(1000, ErrorMessage = "Keep the message to 1000 characters or fewer.")]
        public string Message { get; set; } = string.Empty;

        public DateTime? ExpiresAt { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadAsync();

        if (id is not null)
        {
            var announcement = await _db.Set<Announcement>().AsNoTracking().FirstOrDefaultAsync(a => a.AnnouncementId == id);
            if (announcement is null) return NotFound();

            Input = new InputModel
            {
                AnnouncementId = announcement.AnnouncementId,
                Title = announcement.Title,
                Message = announcement.Message,
                ExpiresAt = announcement.ExpiresAt,
                IsActive = announcement.IsActive
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (Input.ExpiresAt is not null && Input.ExpiresAt <= DateTime.Now)
            ModelState.AddModelError("Input.ExpiresAt", "Choose an expiry time in the future, or leave it empty.");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        Announcement announcement;
        var wasPublished = false;
        if (Input.AnnouncementId == 0)
        {
            announcement = new Announcement
            {
                Title = Input.Title,
                Message = Input.Message,
                ExpiresAt = Input.ExpiresAt,
                IsActive = Input.IsActive,
                CreatedAt = DateTime.Now
            };
            _db.Add(announcement);
            TempData["Message"] = "Announcement posted.";
        }
        else
        {
            var existing = await _db.Set<Announcement>().FirstOrDefaultAsync(a => a.AnnouncementId == Input.AnnouncementId);
            if (existing is null) return NotFound();
            announcement = existing;
            wasPublished = announcement.IsLiveAt(DateTime.Now);

            announcement.Title = Input.Title;
            announcement.Message = Input.Message;
            announcement.ExpiresAt = Input.ExpiresAt;
            announcement.IsActive = Input.IsActive;
            TempData["Message"] = "Announcement saved.";
        }

        await _db.SaveChangesAsync();
        if (!wasPublished)
            await _notifications.AnnouncementPublishedAsync(announcement);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var announcement = await _db.Set<Announcement>().FirstOrDefaultAsync(a => a.AnnouncementId == id);
        if (announcement is null) return NotFound();

        _db.Remove(announcement);
        await _db.SaveChangesAsync();

        TempData["Message"] = "Announcement deleted.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Announcements = await _db.Set<Announcement>()
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }
}
