using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Pages.Dashboards;

/// <summary>/Dashboards: sends a signed-in user to their own role's dashboard. It has nothing to show itself.</summary>
[Authorize]
public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.IsInRole(PaymentRoles.Admin))
            return RedirectToPage("Admin");
        if (User.IsInRole(PaymentRoles.Staff))
            return RedirectToPage("Staff");
        if (User.IsInRole(PaymentRoles.Customer))
            return RedirectToPage("Customer");
        return Forbid();
    }
}
