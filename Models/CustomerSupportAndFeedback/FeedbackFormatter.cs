using System.Globalization;
using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Models.CustomerSupportAndFeedback;

/// <summary>How feedback is written on the pages. The pages call these instead of formatting by hand.</summary>
public static class FeedbackFormatter
{
    /// <summary>Four filled stars and one empty one, for a rating of 4.</summary>
    public static string Stars(int rating)
    {
        var filled = Math.Clamp(rating, 0, 5);
        return new string('★', filled) + new string('☆', 5 - filled);
    }

    /// <summary>The same rating in words, for screen readers.</summary>
    public static string StarsLabel(int rating) =>
        rating == 1 ? "1 star out of 5" : $"{rating} stars out of 5";

    /// <summary>The reference of the order or reservation that was rated, such as ORD-000012.</summary>
    public static string Visit(int? orderId, int? reservationId) =>
        orderId is int order ? OrderFormatter.Reference(order)
        : reservationId is int reservation ? ReservationFormatter.Reference(reservation)
        : "Visit";

    /// <summary>26 Oct 2026, 7:30 PM</summary>
    public static string When(DateTime value) =>
        value.ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>The label of a star filter: "1 star", "5 stars".</summary>
    public static string FilterLabel(int stars) => stars == 1 ? "1 star" : $"{stars} stars";
}
