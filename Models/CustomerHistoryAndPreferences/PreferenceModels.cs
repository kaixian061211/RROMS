using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Models.CustomerHistoryAndPreferences;

/// <summary>The choices the preferences form offers: the menu's categories and the restaurant's dining areas.</summary>
public record PreferenceOptions(IReadOnlyList<string> Categories, IReadOnlyList<string> SeatingAreas);

/// <summary>A customer in the staff list, and whether they have saved any preferences.</summary>
public record CustomerSummary(int UserId, string FullName, string Email, bool IsActive, bool HasPreferences);

/// <summary>One customer's preferences as staff see them. Preference is null when the customer hasn't saved any.</summary>
public record CustomerPreferenceView(int UserId, string FullName, string Email, CustomerPreference? Preference);

/// <summary>A customer's past reservations, orders and payments, newest first, with the tables' numbers.</summary>
public record CustomerHistory(
    IReadOnlyList<Reservation> Reservations,
    IReadOnlyList<OrderDetails> Orders,
    IReadOnlyList<PaymentDetails> Payments,
    IReadOnlyDictionary<int, TableInfo> Tables);

/// <summary>What saving preferences returns. A failure carries a message for the page to show.</summary>
public class PreferenceResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public CustomerPreference? Preference { get; init; }

    public static PreferenceResult Success(CustomerPreference preference) =>
        new() { Succeeded = true, Preference = preference };

    public static PreferenceResult Failure(string errorMessage) =>
        new() { ErrorMessage = errorMessage };
}
