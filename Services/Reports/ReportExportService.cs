using System.Globalization;
using RROMS.Web.Models.Reports;

namespace RROMS.Web.Services.Reports;

/// <summary>Formats the complete report result. No database access or metric calculation.</summary>
public partial class ReportExportService
{
    private sealed record ExportColumn(string Title, string? NumberFormat = null, float Width = 1);
    private sealed record ExportTable(
        string Name, string Basis, ExportColumn[] Columns, IReadOnlyList<object?[]> Rows);

    private const string MoneyFormat = "#,##0.00";
    // Percentages in report results are already on the 0–100 scale.
    private const string PercentFormat = "0.0\"%\"";

    private static string? CellNumberFormat(ExportTable table, object?[] row, int column) =>
        table.Name == "Summary" && column == 2 && Equals(row[3], "%")
            ? "0.0"
            : table.Columns[column].NumberFormat;

    public static string FileName(DashboardReport report, string extension)
    {
        if (extension is not ("pdf" or "xlsx" or "csv"))
            throw new ArgumentOutOfRangeException(nameof(extension));

        var date = report.From.ToString(report.Period switch
        {
            ReportPeriod.Monthly => "yyyy-MM",
            ReportPeriod.Yearly => "yyyy",
            _ => "yyyy-MM-dd"
        }, CultureInfo.InvariantCulture);

        return $"RROMS-Report-{report.Period}-{date}.{extension}";
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "N/A",
        DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm", CultureInfo.InvariantCulture),
        bool flag => flag ? "Yes" : "No",
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static IReadOnlyList<ExportTable> BuildTables(DashboardReport r) =>
    [
        new("Summary", string.Join(Environment.NewLine, r.Notes),
            [new("Section"), new("Metric", Width: 2.5f), new("Value"), new("Unit")],
            [
                ["Sales", "Gross collections", r.Sales.GrossCollections, DashboardReport.Currency],
                ["Sales", "Payments", r.Sales.PaymentCount, "Count"],
                ["Sales", "Confirmed refunds", r.Sales.ConfirmedRefunds, DashboardReport.Currency],
                ["Sales", "Refund count", r.Sales.RefundCount, "Count"],
                ["Sales", "Net collections", r.Sales.NetCollections, DashboardReport.Currency],
                ["Sales", "Average payment", r.Sales.AveragePayment, DashboardReport.Currency],
                ["Orders", "Orders placed", r.Orders.OrdersPlaced, "Count"],
                ["Orders", "Cancelled orders", r.Orders.CancelledOrders, "Count"],
                ["Orders", "Order value", r.Orders.OrderValue, DashboardReport.Currency],
                ["Orders", "Average order value", r.Orders.AverageOrderValue, DashboardReport.Currency],
                ["Reservations", "Scheduled reservations", r.Reservations.ScheduledReservations, "Count"],
                ["Reservations", "Scheduled guests (non-cancelled)", r.Reservations.ScheduledGuests, "Guests"],
                ["Reservations", "New bookings created", r.Reservations.NewBookingsCreated, "Count"],
                ["Reservations", "Currently cancelled scheduled", r.Reservations.CancelledScheduled, "Count"],
                ["Reservations", "Cancellation percentage", r.Reservations.CancellationPercent, "%"],
                ["Utilization", "Available table-slots", r.Utilization.AvailableSlots, "Slots"],
                ["Utilization", "Booked table-slots", r.Utilization.BookedSlots, "Slots"],
                ["Utilization", "Estimated booked-slot utilization", r.Utilization.UtilizationPercent, "%"],
                ["Utilization", "Additional duplicate reservations", r.Utilization.DuplicateBookings, "Reservations"],
                ["Utilization", "Booked slots outside current availability", r.Utilization.UnmatchedBookings, "Distinct slots"],
                ["Menu", "Quantity purchased", r.Menu.TotalQuantity, "Items"],
                ["Menu", "Purchased item amount", r.Menu.TotalAmount, DashboardReport.Currency],
                ["Customers", "Active customers", r.Customers.ActiveCustomers, "Accounts"],
                ["Customers", "Returning customers (estimate)", r.Customers.ReturningCustomers, "Accounts"]
            ]),
        new("Sales Transactions", r.Sales.Basis,
            [new("At (Malaysia)", "yyyy-mm-dd hh:mm:ss", 1.7f), new("Movement"), new("Payment ID"),
             new("Order ID"), new("Customer ID"), new("Method"), new("Amount (MYR)", MoneyFormat),
             new("Reference", Width: 1.5f), new("Note", Width: 2)],
            r.Sales.Transactions.Select(x => new object?[]
                { x.At, x.Type.ToString(), x.PaymentId, x.OrderId, x.CustomerUserId,
                  x.Method, x.Amount, x.Reference, x.Note }).ToList()),
        new("Sales Trend", r.Sales.Basis,
            [new("Interval", Width: 1.8f), new("Collections (MYR)", MoneyFormat), new("Payments"),
             new("Refunds (MYR)", MoneyFormat), new("Net (MYR)", MoneyFormat)],
            r.Sales.Trend.Select(x => new object?[]
                { x.Label, x.Collections, x.PaymentCount, x.Refunds, x.Net }).ToList()),
        new("Payment Methods", r.Sales.Basis,
            [new("Method"), new("Payments"), new("Collections (MYR)", MoneyFormat),
             new("Refunds (MYR)", MoneyFormat), new("Net (MYR)", MoneyFormat), new("Collection share", PercentFormat)],
            r.Sales.Methods.Select(x => new object?[]
                { x.Method, x.PaymentCount, x.Amount, x.Refunds, x.Net, x.SharePercent }).ToList()),
        new("Orders", r.Orders.Basis,
            [new("Order ID"), new("Placed (Malaysia)", "yyyy-mm-dd hh:mm:ss", 1.7f), new("Customer ID"),
             new("Table"), new("Status"), new("Item quantity"), new("Saved total (MYR)", MoneyFormat), new("Payment status")],
            r.Orders.Orders.Select(x => new object?[]
                { x.OrderId, x.PlacedAt, x.CustomerUserId, x.Table, x.Status, x.ItemCount,
                  x.Total, x.PaymentStatus ?? "Not recorded" }).ToList()),
        new("Order Trend", r.Orders.Basis,
            [new("Interval", Width: 1.8f), new("Orders placed"), new("Order value (MYR)", MoneyFormat)],
            r.Orders.Trend.Select(x => new object?[] { x.Label, x.OrdersPlaced, x.OrderValue }).ToList()),
        new("Order Statuses", r.Orders.Basis,
            [new("Current status"), new("Orders")],
            r.Orders.StatusBreakdown.Select(x => new object?[] { x.Status, x.Count }).ToList()),
        new("Reservations", r.Reservations.Basis,
            [new("Reservation ID"), new("Dining date", "yyyy-mm-dd"), new("Seating time", "hh:mm"),
             new("Table"), new("Area", Width: 1.5f), new("Customer ID"), new("Guests"), new("Status"),
             new("Created (Malaysia)", "yyyy-mm-dd hh:mm:ss", 1.7f), new("Special request", Width: 2)],
            r.Reservations.Reservations.Select(x => new object?[]
                { x.ReservationId, x.Date, x.TimeSlot, x.Table, x.Area, x.CustomerUserId,
                  x.PartySize, x.Status, x.CreatedAt, x.SpecialRequest }).ToList()),
        new("Reservation Trend", r.Reservations.Basis,
            [new("Interval", Width: 1.8f), new("Scheduled"), new("Currently cancelled")],
            r.Reservations.Trend.Select(x => new object?[] { x.Label, x.Scheduled, x.Cancelled }).ToList()),
        new("Reservation Statuses", r.Reservations.Basis,
            [new("Current status"), new("Reservations")],
            r.Reservations.StatusBreakdown.Select(x => new object?[] { x.Status, x.Count }).ToList()),
        new("Peak Slots", r.Reservations.Basis,
            [new("Seating time", "hh:mm"), new("Non-cancelled reservations"), new("Guests"), new("Peak")],
            r.Reservations.PeakSlots.Select(x => new object?[] { x.TimeSlot, x.Reservations, x.Guests, x.IsPeak }).ToList()),
        new("Table Utilization", r.Utilization.Basis,
            [new("Table ID"), new("Table"), new("Area", Width: 1.5f), new("Seats"),
             new("Available slots"), new("Booked slots"), new("Estimated utilization", PercentFormat)],
            r.Utilization.Tables.Select(x => new object?[]
                { x.TableId, x.TableNumber, x.Area, x.Capacity, x.AvailableSlots, x.BookedSlots, x.UtilizationPercent }).ToList()),
        new("Menu Items", r.Menu.Basis,
            [new("Rank"), new("Menu item ID"), new("Name", Width: 2), new("Category", Width: 1.5f),
             new("Quantity"), new("Item amount (MYR)", MoneyFormat), new("Amount share", PercentFormat)],
            r.Menu.Items.Select(x => new object?[]
                { x.Rank, x.MenuItemId, x.Name, x.Category, x.Quantity, x.Amount, x.SharePercent }).ToList()),
        new("Customers", r.Customers.Basis,
            [new("Customer ID"), new("Account", Width: 1.5f), new("Bookings created"), new("Orders placed"),
             new("Paid orders"), new("Gross spending (MYR)", MoneyFormat), new("Refunds (MYR)", MoneyFormat),
             new("Net spending (MYR)", MoneyFormat), new("Estimated dining days"), new("Returning (estimate)")],
            r.Customers.Customers.Select(x => new object?[]
                { x.CustomerUserId, x.DisplayName, x.ReservationsMade, x.OrdersPlaced, x.PaidOrders,
                  x.GrossSpending, x.RefundsReceived, x.NetSpending, x.EstimatedDiningDays, x.IsReturning }).ToList())
    ];
}
