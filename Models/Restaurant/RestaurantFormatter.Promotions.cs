using System.Globalization;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Models.Restaurant;

public static partial class RestaurantFormatter
{
    public static StatusPill PromotionStatus(Promotion promotion, DateOnly today)
    {
        if (!promotion.IsActive) return new StatusPill("Inactive", "rr-status--off");
        if (today < promotion.StartDate) return new StatusPill("Scheduled", "rr-status--attention");
        if (today > promotion.EndDate) return new StatusPill("Expired", "rr-status--off");
        return new StatusPill("Running", "rr-status--good");
    }

    /// <summary>"20% off" or "RM 2.00 off".</summary>
    public static string DiscountLabel(Promotion promotion) =>
        promotion.DiscountType == DiscountType.Percentage
            ? $"{promotion.DiscountValue.ToString("0.##", CultureInfo.InvariantCulture)}% off"
            : $"{PaymentFormatter.Money(promotion.DiscountValue)} off";
}
