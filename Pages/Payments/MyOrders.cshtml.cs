using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

/// <summary>
/// TEMPORARY: lists the signed-in customer's orders with their payment so the checkout can be reached.
/// The ordering module will provide the real page - keep its links to /Payments/CheckOut?orderId=...
/// </summary>
[Authorize]
public class MyOrdersModel : PageModel
{
    private readonly IPaymentOrderProvider _orders;
    private readonly IPaymentQueries _queries;

    public MyOrdersModel(IPaymentOrderProvider orders, IPaymentQueries queries)
    {
        _orders = orders;
        _queries = queries;
    }

    public List<OrderLine> Orders { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var orders = (await _orders.GetOrdersForCustomerAsync(User.GetPaymentUserId()))
            .OrderByDescending(o => o.OrderId)
            .ToList();

        var payments = await _queries.GetByOrdersAsync(orders.Select(o => o.OrderId));

        Orders = orders.Select(o =>
        {
            payments.TryGetValue(o.OrderId, out var payment);

            return new OrderLine
            {
                OrderId = o.OrderId,
                TotalAmount = o.TotalAmount,
                PaymentId = payment?.PaymentId,
                Status = payment?.Status,
                IsAwaitingCounter = payment?.IsAwaitingCounter ?? false
            };
        }).ToList();
    }

    public class OrderLine
    {
        public int OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public int? PaymentId { get; set; }
        public string? Status { get; set; }
        public bool IsAwaitingCounter { get; set; }
    }
}