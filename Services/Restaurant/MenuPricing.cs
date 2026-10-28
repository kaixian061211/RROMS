using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Restaurant;

namespace RROMS.Web.Services.Restaurant;

/// <summary>Works out the price a customer pays once running promotions are applied.</summary>
public static class MenuPricing
{
    /// <summary>The promotions running today, with the dishes they apply to.</summary>
    public static Task<List<Promotion>> LoadRunningAsync(AppDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        return db.Set<Promotion>()
            .AsNoTracking()
            .Include(p => p.MenuItems)
            .Where(p => p.IsActive && p.StartDate <= today && p.EndDate >= today)
            .OrderBy(p => p.EndDate)
            .ToListAsync();
    }

    /// <summary>The lowest price for the dish, and the promotion that gives it. Discounts are never added together.</summary>
    public static (decimal Price, Promotion? Promotion) BestPrice(MenuItem item, IEnumerable<Promotion> runningPromotions)
    {
        var best = item.Price;
        Promotion? bestPromotion = null;

        foreach (var promotion in runningPromotions.Where(p => p.MenuItems.Any(m => m.MenuItemId == item.MenuItemId)))
        {
            var discounted = promotion.Apply(item.Price);

            if (discounted < best)
            {
                best = discounted;
                bestPromotion = promotion;
            }
        }

        return (best, bestPromotion);
    }
}
