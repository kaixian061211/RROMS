using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.CustomerHistoryAndPreferences;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerHistoryAndPreferences;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.History;

/// <summary>A customer looks back at their own reservations, orders and payments, one list at a time.</summary>
[Authorize(Roles = PaymentRoles.Customer)]
public class IndexModel : PageModel
{
    public const string ShowReservations = "reservations";
    public const string ShowOrders = "orders";
    public const string ShowPayments = "payments";

    private readonly ICustomerHistoryService _history;

    public IndexModel(ICustomerHistoryService history)
    {
        _history = history;
    }

    /// <summary>Which list to show. Kept in the address, so a link or a refresh keeps the same list.</summary>
    [BindProperty(SupportsGet = true, Name = "show")]
    public string? Show { get; set; }

    public string Section { get; private set; } = ShowReservations;

    public CustomerHistory History { get; private set; } = CustomerHistory.Empty;

    public async Task OnGetAsync()
    {
        Section = Show switch
        {
            ShowOrders => ShowOrders,
            ShowPayments => ShowPayments,
            _ => ShowReservations
        };

        History = await _history.GetMyHistoryAsync(User.GetPaymentUserId());
    }
}
