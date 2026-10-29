using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Models.Notifications;

public class NotificationConfiguration
    : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        var notificationTypes = string.Join(
            ",",
            NotificationTypes.All.Select(type => $"'{type}'"));

        builder.ToTable("Notification", t =>
        {
            t.HasCheckConstraint(
                "CK_Notification_Type",
                $"[Type] IN ({notificationTypes})");

            // Only the relevant business record may be referenced.
            t.HasCheckConstraint(
                "CK_Notification_RelatedRecord",
                "([Type] = 'order' AND [OrderId] IS NOT NULL " +
                "AND [ReservationId] IS NULL AND [PaymentId] IS NULL) OR " +

                "([Type] = 'reservation' AND [ReservationId] IS NOT NULL " +
                "AND [OrderId] IS NULL AND [PaymentId] IS NULL) OR " +

                "([Type] = 'payment' AND [PaymentId] IS NOT NULL " +
                "AND [OrderId] IS NULL AND [ReservationId] IS NULL) OR " +

                "([Type] IN ('promotion','announcement') " +
                "AND [OrderId] IS NULL AND [ReservationId] IS NULL " +
                "AND [PaymentId] IS NULL)");
        });

        builder.Property(n => n.IsRead)
            .HasDefaultValue(false);

        builder.Property(n => n.CreatedAt)
            .HasDefaultValueSql("SYSDATETIME()");

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(n => n.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Reservation>()
            .WithMany()
            .HasForeignKey(n => n.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Payment>()
            .WithMany()
            .HasForeignKey(n => n.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new
        {
            n.UserId,
            n.NotificationId
        });
    }
}