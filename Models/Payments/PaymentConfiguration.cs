using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.Payments;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payment", t =>
        {
            t.HasCheckConstraint("CK_Payments_PaymentMethod",
                "[PaymentMethod] IN ('cash','card','online_banking','e-wallet','pay_at_counter')");
            t.HasCheckConstraint("CK_Payments_PaymentStatus",
                "[PaymentStatus] IN ('pending','paid','failed','refunded')");
            t.HasCheckConstraint("CK_Payments_HasReservationOrOrder",
                "[ReservationId] IS NOT NULL OR [OrderId] IS NOT NULL");
            t.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_Payments_PaidAt",
                "([PaymentStatus] IN ('paid','refunded') AND [PaidAt] IS NOT NULL) " +
                "OR ([PaymentStatus] IN ('pending','failed') AND [PaidAt] IS NULL)");
            t.HasCheckConstraint("CK_Payments_RefundedAt",
                "([PaymentStatus] = 'refunded' AND [RefundedAt] IS NOT NULL) " +
                "OR ([PaymentStatus] <> 'refunded' AND [RefundedAt] IS NULL)");
        });

        builder.Property(p => p.PaymentStatus).HasDefaultValue("pending");
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

        // one payment per order
        builder.HasIndex(p => p.OrderId).IsUnique()
               .HasFilter("[OrderId] IS NOT NULL");

        builder.HasIndex(p => p.ReservationId).IsUnique()
               .HasFilter("[OrderId] IS NULL AND [ReservationId] IS NOT NULL");

        builder.Property(p => p.TransactionRef).HasMaxLength(100);
        builder.HasIndex(p => p.TransactionRef).IsUnique()
       .HasFilter("[TransactionRef] IS NOT NULL");

       builder.HasIndex(p => p.StripePaymentIntentId).IsUnique()
       .HasFilter("[StripePaymentIntentId] IS NOT NULL");
    }
}