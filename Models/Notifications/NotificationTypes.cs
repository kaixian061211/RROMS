namespace RROMS.Web.Models.Notifications;

public static class NotificationTypes
{
    public const string Reservation = "reservation";
    public const string Order = "order";
    public const string Payment = "payment";
    public const string Promotion = "promotion";
    public const string Announcement = "announcement";

    private enum RelatedRecord
    {
        None,
        Order,
        Reservation,
        Payment
    }

    private sealed record Definition(string Label, RelatedRecord RelatedRecord);

    private static readonly IReadOnlyDictionary<string, Definition> Definitions =
        new Dictionary<string, Definition>(StringComparer.Ordinal)
        {
            [Reservation] = new("Reservation", RelatedRecord.Reservation),
            [Order] = new("Order", RelatedRecord.Order),
            [Payment] = new("Payment", RelatedRecord.Payment),
            [Promotion] = new("Promotion", RelatedRecord.None),
            [Announcement] = new("Announcement", RelatedRecord.None)
        };

    public static IReadOnlyCollection<string> All { get; } =
        Definitions.Keys.ToArray();

    public static string Label(string type) =>
        Definitions.TryGetValue(type, out var definition)
            ? definition.Label
            : "Notification";

    public static bool HasValidRelatedRecord(NotificationInput input)
    {
        if (!Definitions.TryGetValue(input.Type, out var definition))
            return false;

        return definition.RelatedRecord switch
        {
            RelatedRecord.Order =>
                input.OrderId is > 0
                && input.ReservationId is null
                && input.PaymentId is null,
            RelatedRecord.Reservation =>
                input.ReservationId is > 0
                && input.OrderId is null
                && input.PaymentId is null,
            RelatedRecord.Payment =>
                input.PaymentId is > 0
                && input.OrderId is null
                && input.ReservationId is null,
            RelatedRecord.None =>
                input.OrderId is null
                && input.ReservationId is null
                && input.PaymentId is null,
            _ => false
        };
    }
}
