using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Services.Payments.Receipts;

public class ReceiptService : IReceiptService
{
    public byte[] Generate(PaymentDetails payment)
    {
        var isRefunded = payment.Status == PaymentStatus.Refunded;

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(30);
                page.DefaultTextStyle(style => style.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Text("RROMS").FontSize(22).Bold().FontColor(Color.FromHex("#0E5A5A"));
                    column.Item().Text(isRefunded ? "Payment receipt - REFUNDED" : "Payment receipt")
                        .FontSize(12).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(16).Column(column =>
                {
                    column.Spacing(6);

                    Line(column, "Receipt no.", payment.ReceiptNo);
                    Line(column, "Date paid", PaymentFormatter.When(payment.PaidAt));
                    Line(column, "Customer", payment.CustomerName);
                    Line(column, "Order", payment.OrderLabel);
                    Line(column, "Payment method", PaymentFormatter.MethodLabel(payment.Method));
                    if (payment.StaffName != "-")
                    {
                        Line(column, "Served by", payment.StaffName);
                    }

                    column.Item().PaddingVertical(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(isRefunded ? "Amount refunded" : "Total paid").FontSize(13).Bold();
                        row.RelativeItem().AlignRight().Text(PaymentFormatter.Money(payment.Amount)).FontSize(13).Bold();
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Thank you for dining with us. Generated ").FontColor(Colors.Grey.Darken1);
                    text.Span(DateTime.Now.ToString("dd MMM yyyy HH:mm"));
                });
            });
        }).GeneratePdf();
    }

    private static void Line(ColumnDescriptor column, string label, string value)
    {
        column.Item().Row(row =>
        {
            row.RelativeItem().Text(label).FontColor(Colors.Grey.Darken1);
            row.RelativeItem().AlignRight().Text(value).SemiBold();
        });
    }
}