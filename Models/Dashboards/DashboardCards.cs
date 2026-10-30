using RROMS.Web.Models.Payments;

namespace RROMS.Web.Models.Dashboards;

/// <summary>
/// Every feature on the role dashboards, in one list. When a feature is built, put its page path here and
/// its card starts linking to it.
/// </summary>
public static class DashboardCards
{
        private const string Customer = PaymentRoles.Customer;
        private const string Staff = PaymentRoles.Staff;
        private const string Admin = PaymentRoles.Admin;

        public static readonly IReadOnlyList<DashboardCard> All = new DashboardCard[]
        {
        new("Register or log in", "Create an account or sign in.", 1, "Ooi", Customer, null),
        new("Reservations", "Book, change or cancel a table.", 5, "Cheah", Customer, "/Reservations"),

        new("Feedback", "Tell the restaurant about your visit.", 7, "Vishalinii", Customer, "/Feedback"),
        new("Profile", "Update your details.", 8, "Ooi", Customer, null),

        new("Feedback", "Tell the restaurant about your visit.", 7, "Vishalinii", Customer, null),
        new("Profile", "Update your details.", 8, "Ooi", Customer, "/Account/Profile"),

        new("Place orders", "Order food and drinks to your table.", 12, "Cheah", Customer, "/Orders"),
        new("Browse menu", "See what's on the menu.", 13, "Lim", Customer, null),
        new("History", "Look back at your past visits.", 15, "Vishalinii", Customer, null),
        new("Live tracking", "Follow your order as it's prepared.", 19, "Chang", Customer, null),
        new("Notifications", "Messages about your bookings and orders.", 20, "Chang", Customer, "/Notifications/Index"),
        new("Chatbot", "Ask a question and get help.", 21, "Vishalinii", Customer, "/Chatbot"),

        new("Record payments", "Take payments at the counter.", 6, "Chang", Staff, "/Payments/Counter"),
        new("Profile", "Update your details.", 9, "Ooi", Staff, "/Account/Profile"),
        new("Manage reservations", "The day's bookings: confirm, change or cancel them.", 11, "Cheah", Staff, "/Reservations/Manage"),
        new("Order queue", "Receive orders and move them through to served.", 17, "Cheah", Staff, "/Orders/Queue"),
        new("Customer preferences", "See what customers like.", 18, "Vishalinii", Staff, null),

        new("Restaurant info and tables", "The restaurant's details and its tables.", 3, "Lim", Admin, null),
        new("Menu items", "Add, change or remove dishes.", 4, "Lim", Admin, null),
        new("Accounts and roles", "Manage user accounts and their roles.", 10, "Ooi", Admin, "/Admin/Accounts/Index"),
        new("Promotions", "Create and run promotions.", 14, "Lim", Admin, null),
        new("Transactions and reports", "Payments, revenue and reports.", 16, "Chang", Admin, null),
        };

        /// <summary>A role's cards in story order. Cards for features not built yet only show in Development.</summary>
        public static IReadOnlyList<DashboardCard> For(string role, bool isDevelopment) =>
            All.Where(card => card.Role == role && (card.IsBuilt || isDevelopment))
                .OrderBy(card => card.Story)
                .ToList();
}
