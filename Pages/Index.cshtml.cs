using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages;

public class IndexModel : PageModel
{
    /// <summary>How many dishes the front page lists; the menu page has the rest.</summary>
    public const int DishesShown = 8;

    private readonly IRestaurantLookup _restaurant;
    private readonly IOrderService _orders;

    public IndexModel(IRestaurantLookup restaurant, IOrderService orders)
    {
        _restaurant = restaurant;
        _orders = orders;
    }

    /// <summary>One of today's seatings: whether the restaurant is open for it, and whether its time has passed.</summary>
    public record Seating(TimeOnly Time, bool IsOpen, bool IsOver);

    public DateOnly Today { get; private set; }
    public OpeningHours? Hours { get; private set; }
    public bool TakingBookings { get; private set; }
    public IReadOnlyList<Seating> Seatings { get; private set; } = Array.Empty<Seating>();
    public IReadOnlyList<MenuItemInfo> Dishes { get; private set; } = Array.Empty<MenuItemInfo>();
    public int DishCount { get; private set; }

    /// <summary>Signed-in users go straight to their dashboard; visitors see today's service and the menu.</summary>
    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Dashboards/Index");

        var now = DateTime.Now;
        Today = DateOnly.FromDateTime(now);
        Hours = await _restaurant.GetOpeningHoursAsync(Today.DayOfWeek);
        TakingBookings = await _restaurant.IsAcceptingReservationsAsync();

        // The same rule as booking (ReservationService): no hours for the day means open; otherwise a
        // seating has to start from opening time up to, but not including, closing time.
        var time = TimeOnly.FromDateTime(now);
        Seatings = ReservationTimeSlots.All
            .Select(slot => new Seating(
                slot,
                Hours is null || (!Hours.IsClosed && slot >= Hours.Opens && slot < Hours.Closes),
                slot <= time))
            .ToList();

        var menu = await _orders.GetMenuAsync();
        DishCount = menu.Count;
        Dishes = menu.Take(DishesShown).ToList();
        return Page();
    }
}
