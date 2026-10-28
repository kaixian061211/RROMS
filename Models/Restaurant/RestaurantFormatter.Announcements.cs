namespace RROMS.Web.Models.Restaurant;

public static partial class RestaurantFormatter
{
    public static StatusPill AnnouncementStatus(Announcement announcement, DateTime now)
    {
        if (!announcement.IsActive) return new StatusPill("Hidden", "rr-status--off");
        if (!announcement.IsLiveAt(now)) return new StatusPill("Expired", "rr-status--off");
        return new StatusPill("Live", "rr-status--good");
    }
}
