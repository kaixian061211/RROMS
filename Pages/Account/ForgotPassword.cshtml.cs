using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Services.Accounts;

namespace RROMS.Web.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly IAccountService _accounts;

    public ForgotPasswordModel(IAccountService accounts)
    {
        _accounts = accounts;
    }

    [BindProperty]
    public ForgotPasswordInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        await _accounts.RequestPasswordResetAsync(Input.Email);

        // Same message whether or not the email is registered, so this page cannot be used to find out who has an account.
        TempData["StatusMessage"] = "If an account exists for that email, we have sent a 6-digit code. It expires in 10 minutes.";
        return RedirectToPage("ResetPassword", new { email = Input.Email });
    }
}
