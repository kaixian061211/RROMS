using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RROMS.Web.Pages;

public class IndexModel : PageModel
{
    /// <summary>Signed-in users go straight to their dashboard; visitors see the front page.</summary>
    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? RedirectToPage("/Dashboards/Index") : Page();
}
