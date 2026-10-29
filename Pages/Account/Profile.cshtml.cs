using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Services.Accounts;

namespace RROMS.Web.Pages.Account;

/// <summary>
/// My profile: used by customers, staff and admins. The user id always comes from the sign-in cookie,
/// never from the URL, so nobody can open or change another person's account here.
/// </summary>
[Authorize]
public class ProfileModel : PageModel
{
    private readonly IAccountService _accounts;

    public ProfileModel(IAccountService accounts)
    {
        _accounts = accounts;
    }

    [BindProperty]
    public ProfileInput Profile { get; set; } = new();

    [BindProperty]
    public ChangePasswordInput Password { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string Role { get; private set; } = string.Empty;

    /// <summary>False for a Development test user: they are not rows in the UserAccount table, so there is no profile to edit.</summary>
    public bool HasAccount { get; private set; } = true;

    public async Task<IActionResult> OnGetAsync()
    {
        HasAccount = await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostProfileAsync()
    {
        if (!User.TryGetUserId(out var userId)) return Unauthorized();

        // Only check the profile form (the password form has its own handler)
        ModelState.Clear();
        TryValidateModel(Profile, nameof(Profile));
        if (!ModelState.IsValid)
        {
            await LoadRoleAsync(userId);
            return Page();
        }

        var result = await _accounts.UpdateProfileAsync(userId, Profile);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("Profile.Email", result.ErrorMessage!);
            await LoadRoleAsync(userId);
            return Page();
        }

        // Refresh the cookie so the name in the navbar updates straight away
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AccountClaims.CreatePrincipal(result.User!));

        StatusMessage = "Profile updated successfully.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        if (!User.TryGetUserId(out var userId)) return Unauthorized();

        ModelState.Clear();
        TryValidateModel(Password, nameof(Password));
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var result = await _accounts.ChangePasswordAsync(userId, Password.CurrentPassword, Password.NewPassword);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("Password.CurrentPassword", result.ErrorMessage!);
            await LoadAsync();
            return Page();
        }

        StatusMessage = "Password changed successfully.";
        return RedirectToPage();
    }

    private async Task<bool> LoadAsync()
    {
        if (!User.TryGetUserId(out var userId)) return false;

        var account = await _accounts.GetByIdAsync(userId);
        if (account is null) return false;

        Profile = new ProfileInput { FullName = account.FullName, Email = account.Email, Phone = account.Phone };
        Role = account.Role;
        return true;
    }

    private async Task LoadRoleAsync(int userId)
    {
        Role = (await _accounts.GetByIdAsync(userId))?.Role ?? string.Empty;
    }
}
