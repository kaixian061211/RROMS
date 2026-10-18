namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>Values stored in Order.Status. They must match the CHECK constraint on the table.</summary>
public static class OrderStatus
{
    public const string Pending = "pending";
    public const string Preparing = "preparing";
    public const string Ready = "ready";
    public const string Served = "served";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static readonly string[] All = { Pending, Preparing, Ready, Served, Completed, Cancelled };
}
