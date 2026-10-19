using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.OrdersAndReservations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservation", t =>
        {
            t.HasCheckConstraint("CK_Reservations_Status",
                "[Status] IN ('pending','confirmed','cancelled','completed')");
            t.HasCheckConstraint("CK_Reservations_PartySize", "[PartySize] > 0");
        });

        builder.Property(r => r.Status).HasDefaultValue(ReservationStatus.Pending);
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

        // no double-booking: one active reservation per table, date and time slot
        builder.HasIndex(r => new { r.TableId, r.ReservationDate, r.TimeSlot }).IsUnique()
               .HasFilter("[Status] <> 'cancelled'");
    }
}
