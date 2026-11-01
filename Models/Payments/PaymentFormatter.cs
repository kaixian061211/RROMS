using System.Globalization;

namespace RROMS.Web.Models.Payments;

/// <summary>Turns stored payment values into the words and colours shown on screen.</summary>
public static class PaymentFormatter
{
    /// <summary>85.5 becomes "RM 85.50".</summary>
    public static string Money(decimal amount) =>
        "RM " + amount.ToString("N2", CultureInfo.InvariantCulture);

    public static string StatusLabel(string status) => status switch
    {
        PaymentStatus.Pending => "Pending",
        PaymentStatus.Paid => "Paid",
        PaymentStatus.Failed => "Failed",
        PaymentStatus.Refunded => "Refunded",
        _ => status
    };

    /// <summary>Bootstrap badge classes.</summary>
    public static string StatusBadge(string status) => status switch
    {
        PaymentStatus.Pending => "text-bg-warning",
        PaymentStatus.Paid => "text-bg-success",
        PaymentStatus.Failed => "text-bg-danger",
        PaymentStatus.Refunded => "text-bg-secondary",
        _ => "text-bg-light"
    };

    /// <summary>Payment-only status pills; legacy Bootstrap classes remain available to Tracking.</summary>
    public static string StatusPill(string status, string? method = null) => "rr-status " + (status switch
    {
        PaymentStatus.Pending when method == PaymentMethod.PayAtCounter => "rr-status--attention",
        PaymentStatus.Pending => "rr-status--progress",
        PaymentStatus.Paid => "rr-status--good",
        PaymentStatus.Failed => "rr-status--bad",
        _ => "rr-status--off"
    });

    public static string StatusText(string status, string? method = null) => status switch
    {
        PaymentStatus.Pending when method == PaymentMethod.PayAtCounter => "Waiting at counter",
        PaymentStatus.Pending when method is not null => "Pending online",
        _ => StatusLabel(status)
    };

    public static string OrderReference(int orderId) => $"ORD-{orderId:D6}";

    public static string MethodLabel(string method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Card => "Card",
        PaymentMethod.OnlineBanking => "Online banking (FPX)",
        PaymentMethod.EWallet => "E-wallet",
        PaymentMethod.PayAtCounter => "Pay at counter",
        _ => method
    };

    /// <summary>UTC payment timestamps shown in Malaysia time; When remains compatible with other modules.</summary>
    public static string WhenUtc(DateTime? value) =>
        value.HasValue ? When(PaymentTime.ToLocal(value.Value)) : "-";

    public static string When(DateTime? value) =>
        value?.ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture) ?? "-";
}