using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Services.Accounts;

namespace RROMS.Web.Pages.Admin.Accounts;

[Authorize(Roles = UserRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly IAccountService _accounts;
    private readonly IAccountMailer _mailer;

    public CreateModel(IAccountService accounts, IAccountMailer mailer)
    {
        _accounts = accounts;
        _mailer = mailer;
    }

    [BindProperty]
    public AdminUserInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // A new account must have a password (when editing it is optional)
        if (string.IsNullOrEmpty(Input.Password))
            ModelState.AddModelError("Input.Password", "Password is required.");

        if (!ModelState.IsValid)
            return Page();

        var result = await _accounts.CreateAsync(Input);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        var user = result.User!;
        var message = $"Account created for {user.FullName} as {user.Role}.";

        // Staff and admin accounts get a welcome email. The account already exists at this point,
        // so a mail problem only adds a warning; it never undoes the creation.
        if (UserRoles.StaffRoles.Contains(user.Role))
        {
            var loginUrl = Url.PageLink("/Account/Login") ?? string.Empty;
            var (subject, body) = AccountEmails.StaffWelcome(user.FullName, user.Email, user.Role, loginUrl);
            var sent = await _mailer.SendAsync(user.Email, user.FullName, subject, body);
            message += sent
                ? " A welcome email was sent."
                : " The welcome email could not be sent (check the Smtp settings).";
        }

        TempData["StatusMessage"] = message;
        return RedirectToPage("Index");
    }
}
