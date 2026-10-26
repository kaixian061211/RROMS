using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.Restaurant;

public class RestaurantTableConfiguration : IEntityTypeConfiguration<RestaurantTable>
{
    public void Configure(EntityTypeBuilder<RestaurantTable> builder)
    {
        builder.ToTable("RestaurantTable", t => t.HasCheckConstraint("CK_RestaurantTable_Capacity", "[Capacity] BETWEEN 1 AND 20"));

        builder.Property(t => t.TableNumber).IsRequired().HasMaxLength(20);
        builder.HasIndex(t => t.TableNumber).IsUnique();

        // an area that still has tables cannot be deleted
        builder.HasOne(t => t.DiningArea)
               .WithMany(a => a.Tables)
               .HasForeignKey(t => t.DiningAreaId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
