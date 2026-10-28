using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Services.Accounts;

namespace RROMS.Web.Pages.Admin.Accounts;

[Authorize(Roles = UserRoles.Admin)]
public class EditModel : PageModel
{
    private readonly IAccountService _accounts;

    public EditModel(IAccountService accounts)
    {
        _accounts = accounts;
    }

    [BindProperty]
    public AdminUserInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var account = await _accounts.GetByIdAsync(id);
        if (account is null) return NotFound();

        Input = new AdminUserInput
        {
            UserId = account.UserId,
            FullName = account.FullName,
            Email = account.Email,
            Phone = account.Phone,
            Role = account.Role,
            IsActive = account.IsActive
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();
        if (!ModelState.IsValid) return Page();

        var result = await _accounts.UpdateAsync(adminId, Input);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        TempData["StatusMessage"] = $"{result.User!.FullName} updated successfully.";
        return RedirectToPage("Index");
    }
}
