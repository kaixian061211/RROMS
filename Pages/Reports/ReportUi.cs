using System;
using System.Collections.Generic;
using System.Globalization;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Pages.Reports;

public sealed record ReportCell(
    string Text,
    string CssClass = "",
    bool Numeric = false);

public sealed record ReportTableModel(
    string Caption,
    string[] Headers,
    IEnumerable<ReportCell[]> Rows,
    string EmptyMessage,
    int? Limit = null);

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

    public static string Date(DateOnly value) =>
        value.ToString("d MMM yyyy", Culture);

    public static string Time(TimeOnly value) =>
        value.ToString("h:mm tt", CultureInfo.InvariantCulture);

    public static string OrderReference(int id) => $"ORD-{id:D6}";

    public static string ReservationReference(int id) => $"RES-{id:D6}";

    public static ReportCell T(string? value) =>
        new(string.IsNullOrWhiteSpace(value) ? "—" : value);

    public static ReportCell N(int value) =>
        new(Count(value), Numeric: true);

    public static ReportCell M(decimal? value) =>
        new(Money(value), "rr-money", Numeric: true);

    public static ReportCell P(decimal? value) =>
        new(Percentage(value), Numeric: true);

    public static ReportCell Ref(string value) =>
        new(value, "rr-ref");

    public static ReportCell State(
        string? value,
        string domain,
        string? label = null)
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

        return new(
            label ?? value ?? "Not recorded",
            $"rr-status rr-status--{modifier}");
    }
}
