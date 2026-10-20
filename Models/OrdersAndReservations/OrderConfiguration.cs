using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RROMS.Web.Models.OrdersAndReservations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Order", t =>
        {
            t.HasCheckConstraint("CK_Orders_Status",
                "[Status] IN ('pending','preparing','ready','served','completed','cancelled')");
        });

        builder.Property(o => o.Status).HasDefaultValue(OrderStatus.Pending);
        builder.Property(o => o.CreatedAt).HasDefaultValueSql("SYSDATETIME()");
    }
}
