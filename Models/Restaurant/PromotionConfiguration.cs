using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.Restaurant;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("Promotion", t =>
        {
            t.HasCheckConstraint("CK_Promotion_DiscountValue", "[DiscountValue] > 0");
            t.HasCheckConstraint("CK_Promotion_Dates", "[EndDate] >= [StartDate]");
        });

        builder.Property(p => p.Title).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Description).IsRequired().HasMaxLength(500);
        builder.Property(p => p.DiscountType).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.DiscountValue).HasPrecision(10, 2);
        builder.Property(p => p.BannerPath).HasMaxLength(200);

        // which dishes a promotion applies to: the MenuItemPromotion join table
        builder.HasMany(p => p.MenuItems)
               .WithMany(m => m.Promotions)
               .UsingEntity<Dictionary<string, object>>(
                   "MenuItemPromotion",
                   toMenuItem => toMenuItem.HasOne<MenuItem>().WithMany().HasForeignKey("MenuItemId"),
                   toPromotion => toPromotion.HasOne<Promotion>().WithMany().HasForeignKey("PromotionId"));
    }
}
