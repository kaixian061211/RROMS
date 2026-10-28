using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.Restaurant;

namespace RROMS.Web.Pages.Promotions.Manage;

/// <summary>Adds a promotion, or edits one when ?id= is given.</summary>
[Authorize(Roles = PaymentRoles.Admin)]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public EditModel(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>The dishes this promotion applies to.</summary>
    [BindProperty]
    public List<int> SelectedMenuItemIds { get; set; } = new();

    [BindProperty]
    public IFormFile? BannerFile { get; set; }

    public List<MenuItem> AllMenuItems { get; private set; } = new();

    /// <summary>The banner already saved, shown above the upload box.</summary>
    public string? BannerPath { get; private set; }

    public class InputModel
    {
        public int PromotionId { get; set; }

        [Required(ErrorMessage = "Enter a title.")]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter a short description.")]
        [StringLength(500, ErrorMessage = "Keep the description to 500 characters or fewer.")]
        public string Description { get; set; } = string.Empty;

        public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

        [Range(0.01, 99999999.99, ErrorMessage = "Enter a discount above zero.")]
        public decimal DiscountValue { get; set; }

        public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(7));

        public bool IsActive { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadMenuItemsAsync();

        if (id is not null)
        {
            var promotion = await _db.Set<Promotion>()
                .AsNoTracking()
                .Include(p => p.MenuItems)
                .FirstOrDefaultAsync(p => p.PromotionId == id);
            if (promotion is null) return NotFound();

            BannerPath = promotion.BannerPath;
            SelectedMenuItemIds = promotion.MenuItems.Select(m => m.MenuItemId).ToList();
            Input = new InputModel
            {
                PromotionId = promotion.PromotionId,
                Title = promotion.Title,
                Description = promotion.Description,
                DiscountType = promotion.DiscountType,
                DiscountValue = promotion.DiscountValue,
                StartDate = promotion.StartDate,
                EndDate = promotion.EndDate,
                IsActive = promotion.IsActive
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.EndDate < Input.StartDate)
            ModelState.AddModelError("Input.EndDate", "The end date cannot be before the start date.");

        if (Input.DiscountType == DiscountType.Percentage && (Input.DiscountValue < 1 || Input.DiscountValue > 99))
            ModelState.AddModelError("Input.DiscountValue", "A percentage discount must be between 1 and 99.");

        if (BannerFile is not null && ImageStorage.Validate(BannerFile) is { } problem)
            ModelState.AddModelError(nameof(BannerFile), problem);

        var chosen = await _db.Set<MenuItem>().Where(m => SelectedMenuItemIds.Contains(m.MenuItemId)).ToListAsync();

        if (chosen.Count == 0)
        {
            ModelState.AddModelError(nameof(SelectedMenuItemIds), "Choose at least one dish for this promotion.");
        }
        else if (Input.DiscountType == DiscountType.FixedAmount)
        {
            var cheapest = chosen.Min(m => m.Price);
            if (Input.DiscountValue >= cheapest)
                ModelState.AddModelError("Input.DiscountValue",
                    $"The discount must be less than the cheapest chosen dish ({PaymentFormatter.Money(cheapest)}).");
        }

        Promotion? promotion = null;
        if (Input.PromotionId != 0)
        {
            promotion = await _db.Set<Promotion>().Include(p => p.MenuItems).FirstOrDefaultAsync(p => p.PromotionId == Input.PromotionId);
            if (promotion is null) return NotFound();
        }

        if (!ModelState.IsValid)
        {
            BannerPath = promotion?.BannerPath;
            await LoadMenuItemsAsync();
            return Page();
        }

        if (promotion is null)
        {
            promotion = new Promotion();
            _db.Add(promotion);
        }

        promotion.Title = Input.Title;
        promotion.Description = Input.Description;
        promotion.DiscountType = Input.DiscountType;
        promotion.DiscountValue = Input.DiscountValue;
        promotion.StartDate = Input.StartDate;
        promotion.EndDate = Input.EndDate;
        promotion.IsActive = Input.IsActive;

        promotion.MenuItems.Clear();
        promotion.MenuItems.AddRange(chosen);

        if (BannerFile is not null)
        {
            ImageStorage.Delete(promotion.BannerPath, _environment.WebRootPath);
            promotion.BannerPath = await ImageStorage.SaveAsync(BannerFile, _environment.WebRootPath, "promotions");
        }

        await _db.SaveChangesAsync();

        TempData["Message"] = Input.PromotionId == 0 ? $"{promotion.Title} added." : $"{promotion.Title} saved.";
        return RedirectToPage("/Promotions/Manage/Index");
    }

    private async Task LoadMenuItemsAsync()
    {
        AllMenuItems = await _db.Set<MenuItem>().AsNoTracking().OrderBy(m => m.Name).ToListAsync();
    }
}
