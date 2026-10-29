namespace RROMS.Web.Models.Restaurant;

public enum DiscountType
{
    Percentage,
    FixedAmount
}

public class Promotion
{
    public int PromotionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

    /// <summary>A percentage (20 means 20% off) or an amount in RM, depending on DiscountType.</summary>
    public decimal DiscountValue { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public string? BannerPath { get; set; }

    public List<MenuItem> MenuItems { get; set; } = new();

    public bool IsRunningOn(DateOnly day) => IsActive && StartDate <= day && day <= EndDate;

    /// <summary>The price after this promotion. Never below RM 0.01, because an order line must cost more than nothing.</summary>
    public decimal Apply(decimal price)
    {
        var discounted = DiscountType == DiscountType.Percentage
            ? price * (1 - DiscountValue / 100m)
            : price - DiscountValue;

        return Math.Max(0.01m, Math.Round(discounted, 2, MidpointRounding.AwayFromZero));
    }
}
