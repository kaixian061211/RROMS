using RROMS.Web.Models.Payments;

namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>
/// One order on the staff queue: the order, its dishes with their names, its payment (null when there is
/// none yet), and the statuses it may move to, so the page only shows those buttons.
/// </summary>
public record OrderQueueItem(
    Order Order,
    IReadOnlyList<OrderLine> Lines,
    PaymentDetails? Payment,
    IReadOnlyList<string> AllowedStatusChanges);
