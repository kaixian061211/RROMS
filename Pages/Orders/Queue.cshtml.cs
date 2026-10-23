using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Web.Pages.Orders;

// TODO: uncomment when the login module exists. [Authorize] fails until authentication has a sign-in
// scheme, so until then User.IsStaff keeps this page to Development.
// [Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class QueueModel : PageModel
{
    private readonly IOrderService _orders;
    private readonly IWebHostEnvironment _environment;

    public QueueModel(IOrderService orders, IWebHostEnvironment environment)
    {
        _orders = orders;
        _environment = environment;
    }

    /// <summary>Empty means every order still to deal with.</summary>
    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusFilter { get; set; }

    public List<OrderQueueItem> Orders { get; private set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.IsStaff(_environment))
            return Unauthorized();

        Orders = await _orders.GetQueueAsync(StatusFilter);
        return Page();
    }

    /// <summary>One of the status buttons. rowVersion is the order's version when the queue was loaded.</summary>
    public async Task<IActionResult> OnPostChangeStatusAsync(int id, string newStatus, byte[] rowVersion)
    {
        if (!User.IsStaff(_environment))
            return Unauthorized();

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
