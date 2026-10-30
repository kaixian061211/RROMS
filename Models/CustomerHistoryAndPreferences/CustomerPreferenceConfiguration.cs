using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.CustomerHistoryAndPreferences;

public class CustomerPreferenceConfiguration : IEntityTypeConfiguration<CustomerPreference>
{
    public void Configure(EntityTypeBuilder<CustomerPreference> builder)
    {
        builder.ToTable("CustomerPreference");

        builder.Property(p => p.UpdatedAt).HasDefaultValueSql("SYSDATETIME()");

        // one preference row per customer
        builder.HasIndex(p => p.CustomerUserId).IsUnique();
    }
}
