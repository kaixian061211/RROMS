using System.Globalization;

namespace RROMS.Web.Models.Restaurant;

public static partial class RestaurantFormatter
{
    /// <summary>26 Oct 2026</summary>
    public static string Date(DateOnly value) =>
        value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>7:30 PM</summary>
    public static string Time(TimeOnly value) =>
        value.ToString("h:mm tt", CultureInfo.InvariantCulture);
}
