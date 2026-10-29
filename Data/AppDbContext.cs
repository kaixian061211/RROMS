using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.Payments;
using RROMS.Web.Models.Notifications;

namespace RROMS.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}