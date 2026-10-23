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

    /// <summary>Bootstrap badge classes.</summary>
    public static string StatusBadge(string status) => status switch
    {
        OrderStatus.Pending => "text-bg-warning",
        OrderStatus.Preparing => "text-bg-info",
        OrderStatus.Ready => "text-bg-primary",
        OrderStatus.Served => "text-bg-dark",
        OrderStatus.Completed => "text-bg-success",
        OrderStatus.Cancelled => "text-bg-secondary",
        _ => "text-bg-light"
    };

    public static string Table(int tableId) => $"Table {tableId}";

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
