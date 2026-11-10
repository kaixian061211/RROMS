using Microsoft.AspNetCore.Authorization;
using RROMS.Web.Models.Dashboards;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Dashboards;

[Authorize(Roles = PaymentRoles.Admin)]
public class AdminModel : DashboardModel
{
    private readonly IRestaurantLookup _restaurant;
    private readonly IMenuItemLookup _menu;

    public AdminModel(IRestaurantLookup restaurant, IMenuItemLookup menu) : base(PaymentRoles.Admin)
    {
        _restaurant = restaurant;
        _menu = menu;
    }

    /// <summary>The staff pages an Admin also uses: reservations, the order queue and counter payments.</summary>
    public IReadOnlyList<DashboardCard> Operations { get; private set; } = Array.Empty<DashboardCard>();

    // How the restaurant is set up, for the "Restaurant setup" facts.
    public int ActiveTables { get; private set; }
    public int DiningAreas { get; private set; }
    public IReadOnlyList<DayOfWeek> ClosedDays { get; private set; } = Array.Empty<DayOfWeek>();
    public int Dishes { get; private set; }
    public int DishesAvailable { get; private set; }
    public bool TakingBookings { get; private set; }
    public bool TakingOrders { get; private set; }

    public override async Task OnGetAsync()
    {
        await base.OnGetAsync();
        Operations = DashboardCards.DailyOperations;

        var tables = (await _restaurant.GetTablesAsync()).Values.Where(t => t.IsActive).ToList();
        ActiveTables = tables.Count;
        DiningAreas = tables.Select(t => t.AreaName).Distinct().Count();

        var closed = new List<DayOfWeek>();
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            if (await _restaurant.GetOpeningHoursAsync(day) is { IsClosed: true })
                closed.Add(day);
        }
        ClosedDays = closed;

        var menu = await _menu.GetAllAsync();
        Dishes = menu.Count;
        DishesAvailable = menu.Count(dish => dish.IsAvailable);

        TakingBookings = await _restaurant.IsAcceptingReservationsAsync();
        TakingOrders = await _restaurant.IsAcceptingOrdersAsync();
    }
}
