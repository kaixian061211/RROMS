using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Services.Accounts;

namespace RROMS.Web.Pages.Admin.Accounts;

[Authorize(Roles = UserRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly IAccountService _accounts;

    public IndexModel(IAccountService accounts)
    {
        _accounts = accounts;
    }

    public List<UserAccount> Accounts { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RoleFilter { get; set; }

    /// <summary>Set by the Create, Edit and activate/deactivate actions.</summary>
    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        Accounts = await _accounts.SearchAsync(Search, RoleFilter);
    }

    // Deactivate / activate instead of delete, so reservation, order and payment history is kept
    public async Task<IActionResult> OnPostToggleAsync(int id, bool activate)
    {
        if (!User.TryGetUserId(out var adminId)) return Unauthorized();

        var result = await _accounts.SetActiveAsync(adminId, id, activate);
        if (result.Succeeded)
            StatusMessage = $"{result.User!.FullName} is now {(activate ? "active" : "deactivated")}.";
        else
            ErrorMessage = result.ErrorMessage;

        return RedirectToPage(new { Search, RoleFilter });
    }
}
