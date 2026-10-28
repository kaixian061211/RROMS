namespace RROMS.Web.Models.Restaurant;

public class Announcement
{
    public int AnnouncementId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Customers stop seeing it after this time. Empty means it stays until switched off.</summary>
    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsLiveAt(DateTime now) => IsActive && (ExpiresAt is null || ExpiresAt > now);
}
