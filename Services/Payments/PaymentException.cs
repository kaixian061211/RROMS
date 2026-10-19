namespace RROMS.Web.Services.Payments;

/// <summary>
/// A business-rule problem whose message is safe to show to the user
/// (for example "This order has already been paid.").
/// </summary>
public class PaymentException : Exception
{
    public PaymentException(string message) : base(message)
    {
    }
}
