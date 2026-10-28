namespace RROMS.Web.Models.Tracking;

public sealed record TrackingSnapshot(
    int? OrderId,
    string? OrderStatus,
    int? ReservationId,
    string? ReservationStatus,
    int? PaymentId,
    string? PaymentStatus);