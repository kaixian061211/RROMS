namespace RROMS.Web.Models.Restaurant;

public static partial class RestaurantFormatter
{
    public static StatusPill TableStatus(bool isActive) =>
        isActive
            ? new StatusPill("In use", "rr-status--good")
            : new StatusPill("Retired", "rr-status--off");
}
