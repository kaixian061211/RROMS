using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RROMS.Web.Pages.Dev;

/// <summary>
/// Development only: every piece of the RROMS design system (wwwroot/css/rroms.css) with real RROMS content,
/// for the team to copy from. Program.cs gives the /Dev pages no address outside Development.
/// </summary>
[AllowAnonymous]
public class StyleGuideModel : PageModel
{
    public void OnGet() { }
}
