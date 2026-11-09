using Microsoft.AspNetCore.Authorization;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Dashboards;

[Authorize(Roles = PaymentRoles.Staff)]
public class StaffModel : DashboardModel
{
    private readonly IReservationService _reservations;
    private readonly IOrderService _orders;

    public StaffModel(IReservationService reservations, IOrderService orders) : base(PaymentRoles.Staff)
    {
        _reservations = reservations;
        _orders = orders;
    }

    /// <summary>Pending bookings from today on: the ones staff still have to confirm.</summary>
    public int BookingsToConfirm { get; private set; }

    /// <summary>How many active orders are at each status (pending to served), for the kitchen counts.</summary>
    public IReadOnlyDictionary<string, int> OrdersByStatus { get; private set; } = new Dictionary<string, int>();

    /// <summary>Today's bookings that aren't cancelled, by time then table.</summary>
    public IReadOnlyList<Reservation> TodaysBookings { get; private set; } = Array.Empty<Reservation>();

    public IReadOnlyDictionary<int, TableInfo> Tables { get; private set; } = new Dictionary<int, TableInfo>();

    public override async Task OnGetAsync()
    {
        await base.OnGetAsync();
        BookingsToConfirm = await _reservations.CountPendingAsync(Today);

        var queue = await _orders.GetQueueAsync(null);
        OrdersByStatus = OrderFormatter.Steps.ToDictionary(
            status => status,
            status => queue.Count(item => item.Order.Status == status));

        TodaysBookings = (await _reservations.GetForDateAsync(Today, null))
            .Where(r => r.Status != ReservationStatus.Cancelled)
            .ToList();
        Tables = await _reservations.GetTablesAsync();
    }
}
