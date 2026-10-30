using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerHistoryAndPreferences;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerHistoryAndPreferences;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.History;

/// <summary>A customer's own past reservations, orders and payments, one list at a time.</summary>
[Authorize(Roles = PaymentRoles.Customer)]
public class IndexModel : PageModel
{
    public const string ReservationsTab = "reservations";
    public const string OrdersTab = "orders";
    public const string PaymentsTab = "payments";

    private readonly ICustomerHistoryService _history;

    public IndexModel(ICustomerHistoryService history)
    {
        _history = history;
    }

    public CustomerHistory History { get; private set; } = null!;

    /// <summary>Which list is showing: one of the three tab names.</summary>
    public string Tab { get; private set; } = ReservationsTab;

    /// <summary>The list comes from the address (?show=orders) so a tap on a tab is a link, and an unknown value shows reservations.</summary>
    public async Task OnGetAsync(string? show)
    {
        Tab = show is OrdersTab or PaymentsTab ? show : ReservationsTab;
        History = await _history.GetAsync(User.GetPaymentUserId());
    }
}
