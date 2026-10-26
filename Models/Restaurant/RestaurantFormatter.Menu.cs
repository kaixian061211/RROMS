namespace RROMS.Web.Models.Restaurant;

/// <summary>Turns stored menu, promotion and restaurant values into the words and colours shown on screen.</summary>
public static partial class RestaurantFormatter
{
    public static StatusPill Availability(bool isAvailable) =>
        isAvailable
            ? new StatusPill("Available", "rr-status--good")
            : new StatusPill("Sold out", "rr-status--off");
}
