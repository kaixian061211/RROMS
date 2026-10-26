using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.Restaurant;

public class MenuCategoryConfiguration : IEntityTypeConfiguration<MenuCategory>
{
    public void Configure(EntityTypeBuilder<MenuCategory> builder)
    {
        builder.ToTable("MenuCategory", t => t.HasCheckConstraint("CK_MenuCategory_DisplayOrder", "[DisplayOrder] >= 0"));

        builder.Property(c => c.Name).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Description).HasMaxLength(200);
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
