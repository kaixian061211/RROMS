using System.Globalization;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>Turns stored order values into the words and colours shown on screen.</summary>
public static class OrderFormatter
{
    /// <summary>Order 11 becomes "ORD-000011".</summary>
    public static string Reference(int orderId) => $"ORD-{orderId:D6}";

    public static string StatusLabel(string status) => status switch
    {
        OrderStatus.Pending => "Pending",
        OrderStatus.Preparing => "Preparing",
        OrderStatus.Ready => "Ready",
        OrderStatus.Served => "Served",
        OrderStatus.Completed => "Completed",
        OrderStatus.Cancelled => "Cancelled",
        _ => status
    };

    /// <summary>The status pill's classes (rr-status, see /Dev/StyleGuide): pending needs attention, preparing is in progress.</summary>
    public static string StatusBadge(string status) => status switch
    {
        OrderStatus.Pending => "rr-status rr-status--attention",
        OrderStatus.Preparing => "rr-status rr-status--progress",
        OrderStatus.Ready => "rr-status rr-status--good",
        OrderStatus.Served or OrderStatus.Completed => "rr-status rr-status--done",
        _ => "rr-status rr-status--off"
    };

    /// <summary>The steps the customer's order page shows, in order. Completed and cancelled aren't steps.</summary>
    public static readonly string[] Steps = { OrderStatus.Pending, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served };

    /// <summary>
    /// How one step looks for an order with this status: "is-done" once the order is past it, "is-current" for
    /// the step it's at, null for steps still to come. A completed order is past them all.
    /// </summary>
    public static string? StepClass(string status, string step)
    {
        var reached = status == OrderStatus.Completed ? Steps.Length : Array.IndexOf(Steps, status);
        var position = Array.IndexOf(Steps, step);
        return position < reached ? "is-done" : position == reached ? "is-current" : null;
    }

    /// <summary>The queue's status filter: finished orders are only listed for today.</summary>
    public static string StatusFilterLabel(string status) =>
        status is OrderStatus.Completed or OrderStatus.Cancelled ? $"{StatusLabel(status)} today" : StatusLabel(status);

    /// <summary>The button text for moving an order to this status.</summary>
    public static string StatusAction(string status) => status switch
    {
        OrderStatus.Preparing => "Start preparing",
        OrderStatus.Ready => "Mark ready",
        OrderStatus.Served => "Mark served",
        OrderStatus.Completed => "Mark completed",
        OrderStatus.Cancelled => "Cancel",
        _ => StatusLabel(status)
    };

    /// <summary>Bootstrap button classes for the status buttons.</summary>
    public static string StatusActionButton(string status) =>
        status == OrderStatus.Cancelled ? "btn-outline-danger" : "btn-primary";

    /// <summary>What the queue shows about the order's payment. No payment, or a failed one, means not paid.</summary>
    public static string PaymentLabel(PaymentDetails? payment) => payment switch
    {
        null => "Not paid",
        { Status: PaymentStatus.Failed } => "Not paid",
        { IsAwaitingCounter: true } => "Waiting at counter",
        { Status: PaymentStatus.Pending } => "Paying online",
        _ => PaymentFormatter.StatusLabel(payment.Status)
    };

    /// <summary>The pill for PaymentLabel: waiting at the counter needs attention, paying online is in progress.</summary>
    public static string PaymentBadge(PaymentDetails? payment) => payment switch
    {
        { IsAwaitingCounter: true } => "rr-status rr-status--attention",
        { Status: PaymentStatus.Pending } => "rr-status rr-status--progress",
        { Status: PaymentStatus.Paid } => "rr-status rr-status--good",
        _ => "rr-status rr-status--off"
    };

    public static string Table(int tableId, IReadOnlyDictionary<int, TableInfo> tables) => ReservationFormatter.Table(tableId, tables);

    /// <summary>"7:45 PM".</summary>
    public static string Time(DateTime value) => value.ToString("h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>How long ago the order was placed: "just now", "12 min ago", "1 h 5 min ago".</summary>
    public static string Waiting(DateTime createdAt)
    {
        var minutes = (int)(DateTime.Now - createdAt).TotalMinutes;
        if (minutes < 1)
            return "just now";
        if (minutes < 60)
            return $"{minutes} min ago";
        return $"{minutes / 60} h {minutes % 60} min ago";
    }

    /// <summary>The payment module's format, so amounts look the same everywhere: "RM 12.90".</summary>
    public static string Money(decimal amount) => PaymentFormatter.Money(amount);

    /// <summary>"21 Oct 2026, 7:45 PM".</summary>
    public static string PlacedAt(DateTime createdAt) =>
        createdAt.ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>How many dishes in total: 2 Nasi Lemak and 3 Teh Tarik is "5 items". Needs the order's Items loaded.</summary>
    public static string ItemCount(Order order)
    {
        var count = order.Items.Sum(item => item.Quantity);
        return count == 1 ? "1 item" : $"{count} items";
    }
}
