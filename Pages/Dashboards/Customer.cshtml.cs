using Microsoft.AspNetCore.Authorization;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Dashboards;

[Authorize(Roles = PaymentRoles.Customer)]
public class CustomerModel : DashboardModel
{
    private readonly IReservationService _reservations;
    private readonly IOrderService _orders;

    public CustomerModel(IReservationService reservations, IOrderService orders) : base(PaymentRoles.Customer)
    {
        _reservations = reservations;
        _orders = orders;
    }

    /// <summary>The customer's soonest booking that hasn't started yet, or null.</summary>
    public Reservation? NextBooking { get; private set; }

    /// <summary>How many upcoming bookings there are, the next one included.</summary>
    public int UpcomingCount { get; private set; }

    /// <summary>The newest order still on its way (pending to served), or null.</summary>
    public OrderDetails? CurrentOrder { get; private set; }

    public IReadOnlyDictionary<int, TableInfo> Tables { get; private set; } = new Dictionary<int, TableInfo>();

    public override async Task OnGetAsync()
    {
        await base.OnGetAsync();
        var customerId = User.GetPaymentUserId();

        var upcoming = await _reservations.GetUpcomingAsync(customerId);
        NextBooking = upcoming.FirstOrDefault();
        UpcomingCount = upcoming.Count;

        var orders = await _orders.GetForCustomerAsync(customerId);
        CurrentOrder = orders.FirstOrDefault(o => OrderFormatter.Steps.Contains(o.Order.Status));

        Tables = await _orders.GetTablesAsync();
    }
}
