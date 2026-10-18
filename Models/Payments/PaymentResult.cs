namespace RROMS.Web.Models.Payments;

public class PaymentResult
{
    public Payment Payment { get; init; } = null!;

    public string? RedirectUrl { get; init; }
}