using RROMS.Web.Models.Payments;

namespace RROMS.Web.Models.Tracking;

public static class TrackingFormatter
{
    public static string PaymentLabel(Payment payment) => payment.PaymentStatus switch
    {
        PaymentStatus.Pending when payment.PaymentMethod == PaymentMethod.PayAtCounter => "Waiting at counter",
        PaymentStatus.Pending => "Awaiting payment",
        _ => PaymentFormatter.StatusLabel(payment.PaymentStatus)
    };

    public static string PaymentStatusClass(Payment payment) => payment.PaymentStatus switch
    {
        PaymentStatus.Pending when payment.PaymentMethod == PaymentMethod.PayAtCounter => "rr-status rr-status--attention",
        PaymentStatus.Pending => "rr-status rr-status--progress",
        PaymentStatus.Paid => "rr-status rr-status--good",
        PaymentStatus.Failed => "rr-status rr-status--bad",
        _ => "rr-status rr-status--off"
    };
}
