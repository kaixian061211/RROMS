using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.CustomerSupportAndFeedback;

public class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder.ToTable("Feedback", t =>
        {
            t.HasCheckConstraint("CK_Feedback_Rating", "[Rating] BETWEEN 1 AND 5");
        });

        builder.Property(f => f.CreatedAt).HasDefaultValueSql("SYSDATETIME()");
        builder.Property(f => f.IsHidden).HasDefaultValue(false);

        // Restrict: deleting an order or reservation must not silently delete a customer's feedback
        builder.HasOne(f => f.Order).WithMany().HasForeignKey(f => f.OrderId)
               .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.Reservation).WithMany().HasForeignKey(f => f.ReservationId)
               .OnDelete(DeleteBehavior.Restrict);

        // one feedback per order, and one per reservation
        builder.HasIndex(f => f.OrderId).IsUnique().HasFilter("[OrderId] IS NOT NULL");
        builder.HasIndex(f => f.ReservationId).IsUnique().HasFilter("[ReservationId] IS NOT NULL");

        // "my feedback" looks feedback up by customer
        builder.HasIndex(f => f.CustomerUserId);
    }
}
