namespace RROMS.Web.Models.Payments;

/// <summary>Payment timestamps are stored in UTC and displayed in restaurant time.</summary>
public static class PaymentTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");
    public static DateTime NowLocal() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
    public static DateTime ToLocal(DateTime storedUtc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(storedUtc, DateTimeKind.Utc), Zone);
    public static DateTime ToUtc(DateTime restaurantLocal) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(restaurantLocal, DateTimeKind.Unspecified), Zone);
}
