using System.Globalization;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Models.CustomerHistoryAndPreferences;

/// <summary>Turns stored history and preference values into the words and colours shown on screen.</summary>
public static class CustomerHistoryFormatter
{
    private const char CategorySeparator = '|';

    /// <summary>Menu category names for storing in one column. Null when none are chosen.</summary>
    public static string? JoinCategories(IEnumerable<string> categories)
    {
        var joined = string.Join(CategorySeparator, categories);
        return joined.Length == 0 ? null : joined;
    }

    /// <summary>The names JoinCategories stored, for showing as a list.</summary>
    public static List<string> SplitCategories(string? stored) =>
        string.IsNullOrWhiteSpace(stored)
            ? new List<string>()
            : stored.Split(CategorySeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>Text for a preference the customer left empty.</summary>
    public static string OrNotSet(string? value) => string.IsNullOrWhiteSpace(value) ? "Not set" : value;

    /// <summary>"21 Oct 2026, 7:45 PM".</summary>
    public static string UpdatedAt(DateTime value) =>
        value.ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>The status pill (rr-status, see /Dev/StyleGuide) for a payment: paid is good, failed went wrong.</summary>
    public static string PaymentBadge(string status) => status switch
    {
        PaymentStatus.Pending => "rr-status rr-status--attention",
        PaymentStatus.Paid => "rr-status rr-status--good",
        PaymentStatus.Failed => "rr-status rr-status--bad",
        _ => "rr-status rr-status--off"
    };

    /// <summary>When a payment happened, in words: "Paid 21 Oct 2026, 7:45 PM", "Refunded ...", or "Started ...".</summary>
    public static string PaymentWhen(PaymentDetails payment) => payment.Status switch
    {
        PaymentStatus.Refunded => "Refunded " + PaymentFormatter.When(payment.RefundedAt ?? payment.PaidAt),
        PaymentStatus.Paid => "Paid " + PaymentFormatter.When(payment.PaidAt),
        _ => "Started " + PaymentFormatter.When(payment.CreatedAt)
    };
}
