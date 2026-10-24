using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RROMS.Web.Pages.Payments;

[AllowAnonymous]
public class DemoModel(IWebHostEnvironment environment) : PageModel
{
    public IActionResult OnGet() => environment.IsDevelopment() ? Page() : NotFound();
}
