using System.Globalization;
using System.Text;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Services.CustomerSupportAndFeedback;

public class ChatContextProvider : IChatContextProvider
{
    private const int RecentOrderCount = 5;

    private static readonly DayOfWeek[] WeekFromMonday =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };

    private readonly IMenuItemLookup _menu;
    private readonly IRestaurantLookup _restaurant;
    private readonly IOrderService _orders;
    private readonly IReservationService _reservations;

    public ChatContextProvider(
        IMenuItemLookup menu,
        IRestaurantLookup restaurant,
        IOrderService orders,
        IReservationService reservations)
    {
        _menu = menu;
        _restaurant = restaurant;
        _orders = orders;
        _reservations = reservations;
    }

    public async Task<string> BuildAsync(int customerUserId)
    {
        var text = new StringBuilder();
        var now = DateTime.Now;

        text.AppendLine($"Today is {now.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture)}. " +
                        $"The time now is {now.ToString("h:mm tt", CultureInfo.InvariantCulture)}.");

        await AddOpeningHoursAsync(text);
        await AddMenuAsync(text);
        await AddCustomerOrdersAsync(text, customerUserId);
        await AddCustomerReservationsAsync(text, customerUserId);

        return text.ToString();
    }

    private async Task AddOpeningHoursAsync(StringBuilder text)
    {
        text.AppendLine();
        text.AppendLine("OPENING HOURS");
        foreach (var day in WeekFromMonday)
        {
            var hours = await _restaurant.GetOpeningHoursAsync(day);
            var description = hours is null ? "not set"
                : hours.IsClosed || hours.Opens is null || hours.Closes is null ? "closed"
                : $"{Time(hours.Opens.Value)} to {Time(hours.Closes.Value)}";
            text.AppendLine($"- {day}: {description}");
        }

        text.AppendLine(await _restaurant.IsAcceptingReservationsAsync()
            ? "New reservations are being accepted."
            : "New reservations are paused right now.");
        text.AppendLine(await _restaurant.IsAcceptingOrdersAsync()
            ? "New orders are being accepted."
            : "New orders are paused right now.");
    }

    private async Task AddMenuAsync(StringBuilder text)
    {
        text.AppendLine();
        text.AppendLine("MENU");
        var items = await _menu.GetAllAsync();
        if (items.Count == 0)
        {
            text.AppendLine("- The menu is empty.");
            return;
        }

        var categories = items
            .GroupBy(item => (item.CategoryDisplayOrder, Name: string.IsNullOrWhiteSpace(item.CategoryName) ? "Other" : item.CategoryName))
            .OrderBy(group => group.Key.CategoryDisplayOrder)
            .ThenBy(group => group.Key.Name);

        foreach (var category in categories)
        {
            text.AppendLine($"{category.Key.Name}:");
            foreach (var item in category)
                text.AppendLine($"- {item.Name}: {Money(item.Price)}{(item.IsAvailable ? string.Empty : " (sold out)")}");
        }
    }

    private async Task AddCustomerOrdersAsync(StringBuilder text, int customerUserId)
    {
        text.AppendLine();
        text.AppendLine("THIS CUSTOMER'S RECENT ORDERS (newest first)");
        var orders = await _orders.GetForCustomerAsync(customerUserId);
        if (orders.Count == 0)
        {
            text.AppendLine("- none");
            return;
        }

        foreach (var details in orders.Take(RecentOrderCount))
        {
            var order = details.Order;
            var lines = string.Join(", ", details.Lines.Select(line => $"{line.Item.Quantity} x {line.MenuItemName}"));
            text.AppendLine($"- {OrderFormatter.Reference(order.OrderId)}, placed {order.CreatedAt.ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture)}, " +
                            $"status {OrderFormatter.StatusLabel(order.Status)}, total {Money(order.Total)}: {lines}");
        }
    }

    private async Task AddCustomerReservationsAsync(StringBuilder text, int customerUserId)
    {
        text.AppendLine();
        text.AppendLine("THIS CUSTOMER'S UPCOMING RESERVATIONS");
        var reservations = await _reservations.GetUpcomingAsync(customerUserId);
        if (reservations.Count == 0)
        {
            text.AppendLine("- none");
            return;
        }

        foreach (var reservation in reservations)
        {
            text.AppendLine($"- {ReservationFormatter.Reference(reservation.ReservationId)}: " +
                            $"{ReservationFormatter.When(reservation.ReservationDate, reservation.TimeSlot)}, " +
                            $"party of {reservation.PartySize}, status {ReservationFormatter.StatusLabel(reservation.Status)}");
        }
    }

    private static string Time(TimeOnly time) => time.ToString("h:mm tt", CultureInfo.InvariantCulture);

    private static string Money(decimal amount) => "RM " + amount.ToString("N2", CultureInfo.InvariantCulture);
}
