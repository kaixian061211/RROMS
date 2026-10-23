namespace RROMS.Web.Services.Payments;

public class PaymentException : Exception
{
    public bool PaymentMayBeInProgress { get; }

    public PaymentException(
        string message,
        bool paymentMayBeInProgress = false) : base(message)
    {
        PaymentMayBeInProgress = paymentMayBeInProgress;
    }
}