using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.CustomerSupportAndFeedback;

public class CustomerRequestConfiguration : IEntityTypeConfiguration<CustomerRequest>
{
    public void Configure(EntityTypeBuilder<CustomerRequest> builder)
    {
        builder.ToTable("CustomerRequest", t =>
        {
            t.HasCheckConstraint("CK_CustomerRequest_Status",
                "[Status] IN ('pending','acknowledged','completed','declined')");
        });

        builder.Property(r => r.Status).HasDefaultValue(RequestStatus.Pending);
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

        builder.HasOne(r => r.Reservation).WithMany().HasForeignKey(r => r.ReservationId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.CustomerUserId);
    }
}
