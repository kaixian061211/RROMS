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

    /// <summary>"Table A2", using the restaurant's table number. A table that's no longer listed shows its id: "Table #4".</summary>
    public static string Table(int tableId, IReadOnlyDictionary<int, TableInfo> tables) => $"Table {TableNumber(tableId, tables)}";

    /// <summary>Just the number, "A2", for where "Table" is already written beside it.</summary>
    public static string TableNumber(int tableId, IReadOnlyDictionary<int, TableInfo> tables) =>
        tables.TryGetValue(tableId, out var table) ? table.TableNumber : $"#{tableId}";

    /// <summary>The tables a form offers: active ones, grouped by dining area, then by number (2 before 10).</summary>
    public static IEnumerable<IGrouping<string, TableInfo>> TablesByArea(IReadOnlyDictionary<int, TableInfo> tables) =>
        tables.Values
            .Where(t => t.IsActive)
            .OrderBy(t => t.AreaName).ThenBy(t => t.TableNumber.Length).ThenBy(t => t.TableNumber)
            .GroupBy(t => t.AreaName);

    public static string Seats(int capacity) => $"seats {capacity}";

    public static string PartySize(int partySize) => partySize == 1 ? "1 person" : $"{partySize} people";

    /// <summary>The staff day list's summary: "4 bookings · 2 pending · 14 people". Cancelled bookings don't count.</summary>
    public static string DaySummary(IEnumerable<Reservation> reservations)
    {
        var booked = reservations.Where(r => r.Status != ReservationStatus.Cancelled).ToList();
        var bookings = booked.Count == 1 ? "1 booking" : $"{booked.Count} bookings";
        var pending = booked.Count(r => r.Status == ReservationStatus.Pending);
        return $"{bookings} · {pending} pending · {PartySize(booked.Sum(r => r.PartySize))}";
    }

    // TODO: show the customer's name once the user table exists.
    public static string Customer(int customerUserId) => $"Customer #{customerUserId}";
}
