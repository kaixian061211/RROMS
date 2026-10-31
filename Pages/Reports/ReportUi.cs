using System;
using System.Globalization;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Pages.Reports;

public static class ReportUi
{
    private static readonly CultureInfo Culture =
        CultureInfo.GetCultureInfo("en-MY");

    public static string Money(decimal? value) =>
        value.HasValue
            ? $"RM {value.Value.ToString("N2", Culture)}"
            : "N/A";

    public static string Count(int value) =>
        value.ToString("N0", Culture);

    public static string Percentage(decimal? value) =>
        value.HasValue
            ? $"{value.Value.ToString("N1", Culture)}%"
            : "N/A";

    // DashboardReport timestamps have already been converted
    // to Malaysia time by ReportService.
    public static string Timestamp(DateTime value) =>
        value.ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);

    public static string DateLabel(DateOnly value) =>
        value.ToString("d MMM yyyy", Culture);

    public static string TimeLabel(TimeOnly value) =>
        value.ToString("h:mm tt", CultureInfo.InvariantCulture);

    public static string OrderReference(int id) => $"ORD-{id:D6}";

    public static string ReservationReference(int id) => $"RES-{id:D6}";

    public static ReportCell T(string? value) =>
        new(string.IsNullOrWhiteSpace(value) ? "—" : value);

    public static ReportCell N(int value) =>
        new(Count(value), Numeric: true);

    public static ReportCell P(decimal? value) =>
        new(Percentage(value), Numeric: true);

    public static string OrderStatusClass(string status) => StatusClass(status, "Order");
    public static string ReservationStatusClass(string status) => StatusClass(status, "Reservation");
    public static string PaymentStatusClass(string? status) => StatusClass(status, "Payment");

    private static string StatusClass(string? value, string domain)
    {
        var modifier = (domain, value) switch
        {
            ("Order", OrderStatus.Pending) => "attention",
            ("Order", OrderStatus.Preparing) => "progress",
            ("Order", OrderStatus.Ready) => "good",
            ("Order", OrderStatus.Served or OrderStatus.Completed) => "done",
            ("Order", OrderStatus.Cancelled) => "off",

            ("Reservation", ReservationStatus.Pending) => "attention",
            ("Reservation", ReservationStatus.Confirmed) => "good",
            ("Reservation", ReservationStatus.Completed) => "done",
            ("Reservation", ReservationStatus.Cancelled) => "off",

            ("Payment", PaymentStatus.Pending) => "attention",
            ("Payment", PaymentStatus.Paid) => "good",
            ("Payment", PaymentStatus.Refunded) => "off",
            ("Payment", PaymentStatus.Failed) => "bad",

            _ => "off"
        };

        return $"rr-status rr-status--{modifier}";
    }

    public static string PaymentMethodLabel(string method) =>
        method switch
        {
            PaymentMethod.Cash => "Cash",
            PaymentMethod.Card => "Card",
            PaymentMethod.OnlineBanking => "Online banking",
            PaymentMethod.EWallet => "E-wallet",
            PaymentMethod.PayAtCounter => "Pay at counter",
            _ => method
        };

    public static string CustomerLabel(int? customerId) =>
        customerId.HasValue
            ? $"Customer #{customerId.Value}"
            : "Unknown";

    public static string OrderStatusLabel(string status) =>
        status switch
        {
            OrderStatus.Pending => "Pending",
            OrderStatus.Preparing => "Preparing",
            OrderStatus.Ready => "Ready",
            OrderStatus.Served => "Served",
            OrderStatus.Completed => "Completed",
            OrderStatus.Cancelled => "Cancelled",
            _ => status
        };

    public static string PaymentStatusLabel(string? status) =>
        status switch
        {
            PaymentStatus.Pending => "Pending",
            PaymentStatus.Paid => "Paid",
            PaymentStatus.Failed => "Failed",
            PaymentStatus.Refunded => "Refunded",
            null => "Not recorded",
            _ => status
        };

    public static string ReservationStatusLabel(string status) =>
        status switch
        {
            ReservationStatus.Pending => "Pending",
            ReservationStatus.Confirmed => "Confirmed",
            ReservationStatus.Cancelled => "Cancelled",
            ReservationStatus.Completed => "Completed",
            _ => status
        };
}
