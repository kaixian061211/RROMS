using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Dev;

namespace RROMS.Web.Pages.Dev;

/// <summary>
/// Development only: sign in as a test user, or sign out. Program.cs gives the /Dev pages no address outside
/// Development, and sends anyone here who needs to sign in, or is signed in without the right role.
/// </summary>
[AllowAnonymous]
public class SwitchUserModel : PageModel
{
    public IReadOnlyList<TestUser> Users => TestUsers.All;

    /// <summary>The page someone tried to open when the sign-in cookie sent them here.</summary>
    public string? ReturnUrl { get; private set; }

    public void OnGet(string? returnUrl) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostSignInAsync(int id)
    {
        var user = TestUsers.Find(id);
        if (user is null)
            return NotFound();

        await HttpContext.SignInAsync(user.ToPrincipal());
        return RedirectToPage("/Dashboards/Index");
    }

    public async Task<IActionResult> OnPostSignOutAsync()
    {
        await HttpContext.SignOutAsync();
        return RedirectToPage();
    }
}
