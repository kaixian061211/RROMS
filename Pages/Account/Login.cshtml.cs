using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Services.Accounts;

namespace RROMS.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly IAccountService _accounts;

    public LoginModel(IAccountService accounts)
    {
        _accounts = accounts;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    /// <summary>Where the user was heading before being sent to the login page.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await _accounts.AuthenticateAsync(Input.Email, Input.Password);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        var user = result.User!;
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AccountClaims.CreatePrincipal(user));

        // Only follow local return URLs (stops "open redirect" attacks)
        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            return LocalRedirect(ReturnUrl);

        // Each role has its own dashboard; /Dashboards sends the user to the right one.
        return RedirectToPage("/Dashboards/Index");
    }
}
