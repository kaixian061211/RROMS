namespace RROMS.Web.Models.CustomerSupportAndFeedback;

/// <summary>What the feedback pages receive, so a page never touches the database entity.</summary>
public record FeedbackView(
    int FeedbackId,
    string DisplayName,
    int Rating,
    string? Comments,
    DateTime CreatedAt,
    string? AdminReply,
    DateTime? RepliedAt,
    bool IsHidden,
    int? OrderId,
    int? ReservationId);

/// <summary>A completed order or reservation the customer hasn't rated yet. Kind is "Order" or "Reservation".</summary>
public record RatableItem(string Kind, int Id, string Label);
