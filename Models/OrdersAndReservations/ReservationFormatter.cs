using System.Globalization;

namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>Turns stored reservation values into the words and colours shown on screen.</summary>
public static class ReservationFormatter
{
    /// <summary>Reservation 8 becomes "RES-000008".</summary>
    public static string Reference(int reservationId) => $"RES-{reservationId:D6}";

    public static string StatusLabel(string status) => status switch
    {
        ReservationStatus.Pending => "Pending",
        ReservationStatus.Confirmed => "Confirmed",
        ReservationStatus.Cancelled => "Cancelled",
        ReservationStatus.Completed => "Completed",
        _ => status
    };

    /// <summary>The status pill's classes (rr-status, see /Dev/StyleGuide): pending needs attention, confirmed is good.</summary>
    public static string StatusBadge(string status) => status switch
    {
        ReservationStatus.Pending => "rr-status rr-status--attention",
        ReservationStatus.Confirmed => "rr-status rr-status--good",
        ReservationStatus.Completed => "rr-status rr-status--done",
        _ => "rr-status rr-status--off"
    };

    /// <summary>The button text for moving a reservation to this status.</summary>
    public static string StatusAction(string status) => status switch
    {
        ReservationStatus.Confirmed => "Confirm",
        ReservationStatus.Completed => "Mark completed",
        ReservationStatus.Cancelled => "Cancel",
        _ => StatusLabel(status)
    };

    /// <summary>Bootstrap button classes for the status buttons.</summary>
    public static string StatusActionButton(string status) => status switch
    {
        ReservationStatus.Confirmed => "btn-success",
        ReservationStatus.Completed => "btn-primary",
        ReservationStatus.Cancelled => "btn-outline-danger",
        _ => "btn-outline-secondary"
    };

    public static string Date(DateOnly date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"Sunday, 18 Oct 2026", for page headings.</summary>
    public static string LongDate(DateOnly date) => date.ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>The yyyy-MM-dd form that date inputs and links use.</summary>
    public static string InputDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The three lines of a date block: "Tue", "27", "Oct".</summary>
    public static (string Day, string Date, string Month) DateBlock(DateOnly date) => (
        date.ToString("ddd", CultureInfo.InvariantCulture),
        date.Day.ToString(CultureInfo.InvariantCulture),
        date.ToString("MMM", CultureInfo.InvariantCulture));

    public static string Time(TimeOnly time) => time.ToString("h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>"01 Dec 2026, 7:30 PM", the same layout as PaymentFormatter.When.</summary>
    public static string When(DateOnly date, TimeOnly time) => $"{Date(date)}, {Time(time)}";

    public static string Table(int tableId) => $"Table {tableId}";

    public static string PartySize(int partySize) => partySize == 1 ? "1 person" : $"{partySize} people";

    // TODO: show the customer's name once the user table exists.
    public static string Customer(int customerUserId) => $"Customer #{customerUserId}";
}
