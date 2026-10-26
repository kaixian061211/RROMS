using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.Restaurant;

public class RestaurantInfoConfiguration : IEntityTypeConfiguration<RestaurantInfo>
{
    public void Configure(EntityTypeBuilder<RestaurantInfo> builder)
    {
        builder.ToTable("RestaurantInfo", t =>
        {
            t.HasCheckConstraint("CK_RestaurantInfo_ServiceCharge", "[ServiceChargePercent] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_RestaurantInfo_Tax", "[TaxPercent] BETWEEN 0 AND 100");
        });

        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Address).IsRequired().HasMaxLength(250);
        builder.Property(r => r.Phone).IsRequired().HasMaxLength(20);
        builder.Property(r => r.Email).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Description).HasMaxLength(1000);
        builder.Property(r => r.LogoPath).HasMaxLength(200);
        builder.Property(r => r.ServiceChargePercent).HasPrecision(5, 2);
        builder.Property(r => r.TaxPercent).HasPrecision(5, 2);
    }
}
