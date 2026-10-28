using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.Restaurant;

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("Announcement");

        builder.Property(a => a.Title).IsRequired().HasMaxLength(150);
        builder.Property(a => a.Message).IsRequired().HasMaxLength(1000);
    }
}
