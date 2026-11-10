using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Services.Accounts;

namespace RROMS.Web.Pages.Dev;

/// <summary>
/// Development only: sign in as any active account in the database without its password, or sign out. Program.cs
/// gives the /Dev pages no address outside Development, and sends anyone here who needs to sign in, or is signed in
/// without the right role.
/// </summary>
[AllowAnonymous]
public class SwitchUserModel : PageModel
{
    private static readonly string[] RoleOrder = { UserRoles.Admin, UserRoles.Staff, UserRoles.Customer };

    private readonly IAccountService _accounts;
    private readonly IWebHostEnvironment _environment;

    public SwitchUserModel(IAccountService accounts, IWebHostEnvironment environment)
    {
        _accounts = accounts;
        _environment = environment;
    }

    /// <summary>The active accounts in this database, admins first, then staff, then customers.</summary>
    public IReadOnlyList<UserAccount> Accounts { get; private set; } = Array.Empty<UserAccount>();

    /// <summary>The page someone tried to open when the sign-in cookie sent them here.</summary>
    public string? ReturnUrl { get; private set; }

    public async Task OnGetAsync(string? returnUrl)
    {
        ReturnUrl = returnUrl;
        Accounts = (await _accounts.SearchAsync(search: null, role: null))
            .Where(account => account.IsActive)
            .OrderBy(account => Array.IndexOf(RoleOrder, account.Role))
            .ThenBy(account => account.UserId)
            .ToList();
    }

    /// <summary>
    /// Signs in as an account without its password, with the same claims /Account/Login issues. This skips the
    /// password, so it also checks the environment itself rather than relying only on the route being removed.
    /// </summary>
    public async Task<IActionResult> OnPostSignInAsync(int id)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        var account = await _accounts.GetByIdAsync(id);
        if (account is null || !account.IsActive)
            return NotFound();

        await HttpContext.SignInAsync(AccountClaims.CreatePrincipal(account));
        return RedirectToPage("/Dashboards/Index");
    }

    public async Task<IActionResult> OnPostSignOutAsync()
    {
        await HttpContext.SignOutAsync();
        return RedirectToPage();
    }
}
