using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Services.Accounts;

namespace RROMS.Web.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly IAccountService _accounts;

    public RegisterModel(IAccountService accounts)
    {
        _accounts = accounts;
    }

    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Index");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await _accounts.RegisterCustomerAsync(Input);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("Input.Email", result.ErrorMessage!);
            return Page();
        }

        TempData["StatusMessage"] = "Registration successful. You can now log in.";
        return RedirectToPage("Login");
    }
}
