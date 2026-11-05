using RROMS.Web.Models.Payments;

namespace RROMS.Web.Models.Dashboards;

/// <summary>
/// Every feature on the role dashboards, in one list. When a feature is built, put its page path here and
/// its card starts linking to it; until then it shows as a "Coming soon" placeholder.
/// </summary>
public static class DashboardCards
{
    private const string Customer = PaymentRoles.Customer;
    private const string Staff = PaymentRoles.Staff;
    private const string Admin = PaymentRoles.Admin;

    public static readonly IReadOnlyList<DashboardCard> All = new DashboardCard[]
    {
        new("Reservations", "Book, change or cancel a table.", 5, "Cheah", Customer, "/Reservations"),
        new("Feedback", "Tell the restaurant about your visit.", 7, "Vishalinii", Customer, null),
        new("Profile", "Update your details.", 8, "Ooi", Customer, "/Account/Profile"),
        new("Place orders", "Order food and drinks to your table.", 12, "Cheah", Customer, "/Orders"),
        new("Browse menu", "See what's on the menu.", 13, "Lim", Customer, "/Menu"),
        new("History", "Look back at your past visits.", 15, "Vishalinii", Customer, null),
        new("Live tracking", "Follow your orders and bookings as their status changes.", 19, "Chang", Customer, "/Tracking"),
        new("Notifications", "Messages about your bookings and orders.", 20, "Chang", Customer, "/Notifications/Index"),
        new("Chatbot", "Ask a question and get help.", 21, "Vishalinii", Customer, null),

        new("Record payments", "Take payments at the counter.", 6, "Chang", Staff, "/Payments/Counter"),
        new("Profile", "Update your details.", 9, "Ooi", Staff, "/Account/Profile"),
        new("Manage reservations", "The day's bookings: confirm, change or cancel them.", 11, "Cheah", Staff, "/Reservations/Manage"),
        new("Order queue", "Receive orders and move them through to served.", 17, "Cheah", Staff, "/Orders/Queue"),
        new("Customer preferences", "See what customers like.", 18, "Vishalinii", Staff, null),

        new("Restaurant info and tables", "The restaurant's details, opening hours, dining areas and tables.", 3, "Lim", Admin, "/Restaurant/Manage"),
        new("Announcements", "News for every customer.", 3, "Lim", Admin, "/Restaurant/Manage/Announcements"),
        new("Menu items", "Add, change or remove dishes and their categories.", 4, "Lim", Admin, "/Menu/Manage"),
        new("Accounts and roles", "Manage user accounts and their roles.", 10, "Ooi", Admin, "/Admin/Accounts/Index"),
        new("Promotions", "Create and run promotions.", 14, "Lim", Admin, "/Promotions/Manage"),
        new("Transactions and reports", "Payments, revenue and reports.", 16, "Chang", Admin, "/Reports"),
    };

    /// <summary>A role's cards in story order, built or not.</summary>
    public static IReadOnlyList<DashboardCard> For(string role) =>
        All.Where(card => card.Role == role)
            .OrderBy(card => card.Story)
            .ToList();

    /// <summary>The staff features an Admin also runs, shown under "Daily operations" on the Admin dashboard.</summary>
    public static IReadOnlyList<DashboardCard> DailyOperations =>
        For(Staff).Where(card => card.Story is 6 or 11 or 17).ToList();
}
