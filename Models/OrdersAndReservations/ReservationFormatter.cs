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

    /// <summary>Bootstrap badge classes.</summary>
    public static string StatusBadge(string status) => status switch
    {
        ReservationStatus.Pending => "text-bg-warning",
        ReservationStatus.Confirmed => "text-bg-success",
        ReservationStatus.Cancelled => "text-bg-secondary",
        ReservationStatus.Completed => "text-bg-primary",
        _ => "text-bg-light"
    };

    public static string Date(DateOnly date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    public static string Time(TimeOnly time) => time.ToString("h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>"01 Dec 2026, 7:30 PM", the same layout as PaymentFormatter.When.</summary>
    public static string When(DateOnly date, TimeOnly time) => $"{Date(date)}, {Time(time)}";

    public static string Table(int tableId) => $"Table {tableId}";

    public static string PartySize(int partySize) => partySize == 1 ? "1 person" : $"{partySize} people";
}
