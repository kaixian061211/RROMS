using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

[Authorize]
public class CheckoutModel : PageModel
{
    private readonly IPaymentService _payments;
    private readonly IPaymentQueries _queries;
    private readonly IPaymentOrderProvider _orders;

    public CheckoutModel(IPaymentService payments, IPaymentQueries queries, IPaymentOrderProvider orders)
    {
        _payments = payments;
        _queries = queries;
        _orders = orders;
    }

    public record MethodOption(string Value, string Title, string Description);

    public static readonly IReadOnlyList<MethodOption> MethodOptions = new[]
    {
        new MethodOption(PaymentMethod.Card, "Card", "Visa or Mastercard, paid securely on Stripe."),
        new MethodOption(PaymentMethod.OnlineBanking, "Online banking", "Pay with FPX from your Malaysian bank account."),
        new MethodOption(PaymentMethod.EWallet, "E-wallet", "Pay with GrabPay."),
        // Pay at the counter: a pending payment that staff record when the customer pays them.
        new MethodOption(PaymentMethod.PayAtCounter, "Pay at the counter", "Place your order now and pay our staff when you arrive.")
    };

    [BindProperty(SupportsGet = true)]
    public int OrderId { get; set; }

    [BindProperty]
    public string Method { get; set; } = PaymentMethod.Card;

    public PaymentOrderInfo? Order { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Order = await FindOrderAsync();
        if (Order is null)
        {
            return NotFound();
        }

        var existing = await _queries.GetByOrderAsync(OrderId);
        if (existing?.Status == PaymentStatus.Paid)
        {
            TempData["Info"] = "This order has already been paid.";
            return RedirectToPage("MyOrders");
        }

        if (existing?.Status == PaymentStatus.Refunded)
        {
            TempData["Info"] = "This order's payment was refunded. Please speak to the restaurant.";
            return RedirectToPage("MyOrders");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _payments.StartCheckoutAsync(OrderId, User.GetPaymentUserId(), Method, baseUrl);

            if (result.RedirectUrl is not null)
            {
                return Redirect(result.RedirectUrl);   // off to Stripe
            }

            return RedirectToPage("Success", new { paymentId = result.Payment.PaymentId });
        }
        catch (PaymentException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToPage(new { orderId = OrderId });
        }
    }

    private async Task<PaymentOrderInfo?> FindOrderAsync()
    {
        var order = await _orders.GetOrderAsync(OrderId);
        return order is not null && order.CustomerUserId == User.GetPaymentUserId() ? order : null;
    }
}