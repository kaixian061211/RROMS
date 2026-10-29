using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.Restaurant;

public class OperatingHourConfiguration : IEntityTypeConfiguration<OperatingHour>
{
    public void Configure(EntityTypeBuilder<OperatingHour> builder)
    {
        builder.ToTable("OperatingHour", t => t.HasCheckConstraint(
            "CK_OperatingHour_Times",
            "[IsClosed] = 1 OR ([OpenTime] IS NOT NULL AND [CloseTime] IS NOT NULL AND [CloseTime] > [OpenTime])"));

        builder.HasIndex(h => h.DayOfWeek).IsUnique();
    }
}
