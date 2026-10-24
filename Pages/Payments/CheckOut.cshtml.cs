using System.ComponentModel.DataAnnotations;
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
    private readonly ILogger<CheckoutModel> _logger;

    public CheckoutModel(
        IPaymentService payments,
        IPaymentQueries queries,
        IPaymentOrderProvider orders,
        ILogger<CheckoutModel> logger)
    {
        _payments = payments;
        _queries = queries;
        _orders = orders;
        _logger = logger;
    }

    public record MethodOption(
        string Value,
        string Title,
        string Description);

    public static readonly IReadOnlyList<MethodOption> MethodOptions =
        new[]
        {
            new MethodOption(
                PaymentMethod.Card,
                "Card",
                "Visa or Mastercard, paid securely on Stripe."),

            new MethodOption(
                PaymentMethod.OnlineBanking,
                "Online banking",
                "Pay with FPX from your Malaysian bank account."),

            new MethodOption(
                PaymentMethod.EWallet,
                "E-wallet",
                "Pay with GrabPay."),

            new MethodOption(
                PaymentMethod.PayAtCounter,
                "Pay at the counter",
                "Place your order now and pay our staff when you arrive.")
        };

    [BindProperty(SupportsGet = true)]
    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "Choose a valid order.")]
    public int OrderId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Choose a payment method.")]
    [StringLength(20)]
    public string Method { get; set; } = PaymentMethod.Card;

    public PaymentOrderInfo Order { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.TryGetPaymentUserId(out var customerId) ||
            customerId <= 0)
        {
            return Forbid();
        }

        if (!ModelState.IsValid || OrderId <= 0)
        {
            return BadRequest("Choose a valid order.");
        }

        try
        {
            var order = await FindOrderAsync(customerId);

            if (order is null)
            {
                return NotFound();
            }

            Order = order;

            if (order.OrderStatus ==
                RROMS.Web.Models.OrdersAndReservations.OrderStatus.Cancelled)
            {
                TempData["Error"] =
                    "Cancelled orders cannot be paid.";

                return RedirectToPage("MyOrders");
            }

            var existing = await _queries.GetByOrderAsync(OrderId);

            if (existing?.Status is
                PaymentStatus.Paid or PaymentStatus.Refunded)
            {
                TempData["Info"] =
                    "This order has already been paid or refunded.";

                return RedirectToPage("MyOrders");
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not load checkout for order {OrderId}",
                OrderId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "Checkout could not be loaded. Please try again later.");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!User.TryGetPaymentUserId(out var customerId) ||
            customerId <= 0)
        {
            return Forbid();
        }

        if (OrderId <= 0 ||
            ModelState[nameof(OrderId)]?.Errors.Count > 0)
        {
            return BadRequest("Choose a valid order.");
        }

        try
        {
            var order = await FindOrderAsync(customerId);

            if (order is null)
            {
                return NotFound();
            }

            Order = order;

            if (order.OrderStatus ==
                RROMS.Web.Models.OrdersAndReservations.OrderStatus.Cancelled)
            {
                TempData["Error"] =
                    "Cancelled orders cannot be paid.";

                return RedirectToPage("MyOrders");
            }

            if (!PaymentMethod.CheckoutChoices.Contains(Method))
            {
                ModelState.AddModelError(
                    nameof(Method),
                    "Choose a supported payment method.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var baseUrl =
                $"{Request.Scheme}://{Request.Host}";

            var result = await _payments.StartCheckoutAsync(
                OrderId,
                customerId,
                Method,
                baseUrl);

            if (result.RedirectUrl is not null)
            {
                return Redirect(result.RedirectUrl);
            }

            return RedirectToPage(
                "Success",
                new
                {
                    paymentId = result.Payment.PaymentId
                });
        }
        catch (PaymentException ex)
        {
            if (Order is null)
            {
                return BadRequest(ex.Message);
            }

            ModelState.AddModelError(
                string.Empty,
                ex.Message);

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Checkout failed for order {OrderId}",
                OrderId);

            const string message =
                "Checkout could not be completed. Please check your payment status before trying again.";

            if (Order is null)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    message);
            }

            Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            ModelState.AddModelError(
                string.Empty,
                message);

            return Page();
        }
    }

    private async Task<PaymentOrderInfo?> FindOrderAsync(
        int customerId)
    {
        var order = await _orders.GetOrderAsync(OrderId);

        return order is not null &&
               order.CustomerUserId == customerId
            ? order
            : null;
    }
}