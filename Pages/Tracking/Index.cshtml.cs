using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Tracking;

[Authorize(Roles = PaymentRoles.Customer)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public Order? TrackedOrder { get; private set; }
    public Reservation? TrackedReservation { get; private set; }
    public Payment? TrackedPayment { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        int? orderId,
        int? reservationId,
        int? paymentId,
        CancellationToken ct)
    {
        if (!User.TryGetPaymentUserId(out var userId) || userId <= 0)
            return Forbid();

        var linkCount = (orderId.HasValue ? 1 : 0)
            + (reservationId.HasValue ? 1 : 0)
            + (paymentId.HasValue ? 1 : 0);

        if (!ModelState.IsValid || linkCount != 1
            || orderId is <= 0
            || reservationId is <= 0
            || paymentId is <= 0)
        {
            return BadRequest();
        }

        if (reservationId is int selectedReservationId)
        {
            TrackedReservation = await _db.Set<Reservation>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    r => r.ReservationId == selectedReservationId
                        && r.CustomerUserId == userId,
                    ct);

            return TrackedReservation is null ? NotFound() : Page();
        }

        if (paymentId is int selectedPaymentId)
        {
            var result = await (
                from payment in _db.Payments.AsNoTracking()
                join order in _db.Set<Order>().AsNoTracking()
                    on payment.OrderId equals order.OrderId
                where payment.PaymentId == selectedPaymentId
                    && order.CustomerUserId == userId
                select new { Order = order, Payment = payment })
                .SingleOrDefaultAsync(ct);

            if (result is null)
                return NotFound();

            TrackedOrder = result.Order;
            TrackedPayment = result.Payment;

            return Page();
        }

        TrackedOrder = await _db.Set<Order>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                o => o.OrderId == orderId
                    && o.CustomerUserId == userId,
                ct);

        if (TrackedOrder is null)
            return NotFound();

        TrackedPayment = await _db.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.OrderId == TrackedOrder.OrderId,
                ct);

        return Page();
    }
}