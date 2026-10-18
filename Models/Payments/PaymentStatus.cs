namespace RROMS.Web.Models.Payments;

/// <summary>Values stored in Payment.PaymentStatus. They must match the CHECK constraint on the table.</summary>
public static class PaymentStatus
{
    public const string Pending = "pending";
    public const string Paid = "paid";
    public const string Failed = "failed";
    public const string Refunded = "refunded";

    public static readonly string[] All = { Pending, Paid, Failed, Refunded };
}