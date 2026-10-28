using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Orders;

[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class QueueModel : PageModel
{
    private readonly IOrderService _orders;

    public QueueModel(IOrderService orders)
    {
        _orders = orders;
    }

    /// <summary>Empty means every order still to deal with.</summary>
    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusFilter { get; set; }

    public List<OrderQueueItem> Orders { get; private set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    /// <summary>Every restaurant table by id, for table numbers (and on forms, the tables to choose from).</summary>
    public IReadOnlyDictionary<int, TableInfo> Tables { get; private set; } = new Dictionary<int, TableInfo>();

    public async Task<IActionResult> OnGetAsync()
    {
        Tables = await _orders.GetTablesAsync();
        Orders = await _orders.GetQueueAsync(StatusFilter);
        return Page();
    }

    /// <summary>One of the status buttons. rowVersion is the order's version when the queue was loaded.</summary>
    public async Task<IActionResult> OnPostChangeStatusAsync(int id, string newStatus, byte[] rowVersion)
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Something went wrong. Please try again.";
        }
        else
        {
            var result = await _orders.ChangeStatusAsync(id, newStatus, rowVersion);
            if (result.Succeeded)
                StatusMessage = $"{OrderFormatter.Reference(id)} is now {OrderFormatter.StatusLabel(newStatus).ToLowerInvariant()}.";
            else
                ErrorMessage = result.ErrorMessage;
        }

        return RedirectToPage(new { status = StatusFilter });
    }
}
