using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.Restaurant;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItem", t => t.HasCheckConstraint("CK_MenuItem_Price", "[Price] > 0"));

        builder.Property(m => m.Name).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Description).IsRequired().HasMaxLength(500);
        builder.Property(m => m.Price).HasPrecision(10, 2);
        builder.Property(m => m.ImagePath).HasMaxLength(200);

        // deleting a category keeps its dishes; they just have no category
        builder.HasOne(m => m.MenuCategory)
               .WithMany(c => c.MenuItems)
               .HasForeignKey(m => m.MenuCategoryId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
