using RROMS.Web.Models.Payments;

namespace RROMS.Web.Services.Payments.Receipts;

public interface IReceiptService
{
    /// <summary>Builds the PDF receipt for a paid (or refunded) payment.</summary>
    byte[] Generate(PaymentDetails payment);
}