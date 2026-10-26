using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.Restaurant;

namespace RROMS.Web.Pages.Restaurant.Manage;

[Authorize(Roles = PaymentRoles.Admin)]
public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public DetailsModel(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? LogoFile { get; set; }

    /// <summary>The logo already saved, shown above the upload box.</summary>
    public string? LogoPath { get; private set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Enter the restaurant name.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the address.")]
        [StringLength(250)]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter a phone number.")]
        [Phone(ErrorMessage = "Enter a valid phone number.")]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter an email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Keep the description to 1000 characters or fewer.")]
        public string? Description { get; set; }

        [Range(0, 100, ErrorMessage = "Service charge must be between 0 and 100.")]
        public decimal ServiceChargePercent { get; set; }

        [Range(0, 100, ErrorMessage = "Tax must be between 0 and 100.")]
        public decimal TaxPercent { get; set; }

        public bool IsAcceptingOrders { get; set; } = true;

        public bool IsAcceptingReservations { get; set; } = true;
    }

    public async Task OnGetAsync()
    {
        var info = await _db.Set<RestaurantInfo>().AsNoTracking().FirstOrDefaultAsync();
        if (info is null) return;

        LogoPath = info.LogoPath;
        Input = new InputModel
        {
            Name = info.Name,
            Address = info.Address,
            Phone = info.Phone,
            Email = info.Email,
            Description = info.Description,
            ServiceChargePercent = info.ServiceChargePercent,
            TaxPercent = info.TaxPercent,
            IsAcceptingOrders = info.IsAcceptingOrders,
            IsAcceptingReservations = info.IsAcceptingReservations
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (LogoFile is not null && ImageStorage.Validate(LogoFile) is { } problem)
            ModelState.AddModelError(nameof(LogoFile), problem);

        var info = await _db.Set<RestaurantInfo>().FirstOrDefaultAsync();

        if (!ModelState.IsValid)
        {
            LogoPath = info?.LogoPath;
            return Page();
        }

        if (info is null)
        {
            info = new RestaurantInfo();
            _db.Add(info);
        }

        info.Name = Input.Name;
        info.Address = Input.Address;
        info.Phone = Input.Phone;
        info.Email = Input.Email;
        info.Description = Input.Description;
        info.ServiceChargePercent = Input.ServiceChargePercent;
        info.TaxPercent = Input.TaxPercent;
        info.IsAcceptingOrders = Input.IsAcceptingOrders;
        info.IsAcceptingReservations = Input.IsAcceptingReservations;

        if (LogoFile is not null)
        {
            ImageStorage.Delete(info.LogoPath, _environment.WebRootPath);
            info.LogoPath = await ImageStorage.SaveAsync(LogoFile, _environment.WebRootPath, "restaurant");
        }

        await _db.SaveChangesAsync();

        TempData["Message"] = "Restaurant details saved.";
        return RedirectToPage();
    }
}
