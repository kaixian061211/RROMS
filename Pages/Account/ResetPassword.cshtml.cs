using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Services.Accounts;

namespace RROMS.Web.Pages.Account;

public class ResetPasswordModel : PageModel
{
    private readonly IAccountService _accounts;

    public ResetPasswordModel(IAccountService accounts)
    {
        _accounts = accounts;
    }

    [BindProperty]
    public ResetPasswordInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    /// <summary>The email is passed from the "Forgot password" page so the user does not have to type it twice.</summary>
    public void OnGet(string? email)
    {
        Input.Email = email ?? string.Empty;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await _accounts.ResetPasswordWithOtpAsync(Input.Email, Input.Otp, Input.NewPassword);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        TempData["StatusMessage"] = "Your password has been reset. You can now log in with your new password.";
        return RedirectToPage("Login");
    }
}
