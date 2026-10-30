using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Pages.Restaurant.Manage;

[Authorize(Roles = PaymentRoles.Admin)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
