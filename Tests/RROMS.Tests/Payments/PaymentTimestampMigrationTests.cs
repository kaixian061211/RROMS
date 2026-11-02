using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using RROMS.Web.Data;
using RROMS.Web.Data.Migrations;
using RROMS.Web.Models.Payments;

namespace RROMS.Tests.Payments;

[Trait("Type", "Integration")]
public class PaymentTimestampMigrationTests
{
    [Fact]
    public async Task LegacyMalaysiaTimestampsConvertTogetherAndDownRestoresThem()
    {
        await TestDatabase.PrepareOnceAsync();
        await using var db = NewContext();
        await db.Database.OpenConnectionAsync();
        TestDatabase.CheckIsTestDatabase((SqlConnection)db.Database.GetDbConnection());
        await using var transaction = await db.Database.BeginTransactionAsync();
        var created = new DateTime(2026, 10, 30, 23, 50, 0);
        var paid = new DateTime(2026, 10, 31, 0, 10, 0);
        var refunded = new DateTime(2026, 11, 1, 1, 30, 0);
        var payment = new Payment { OrderId = 1900000001, Amount = 85.50m, PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Refunded, CreatedAt = created, PaidAt = paid, RefundedAt = refunded, RecordedByStaffId = 3 };
        db.Add(payment);
        await db.SaveChangesAsync();
        await ApplyAsync(db, new ExposedMigration().Operations(up: true));
        db.ChangeTracker.Clear();
        var converted = await db.Payments.AsNoTracking().SingleAsync(p => p.PaymentId == payment.PaymentId);
        Assert.Equal(created.AddHours(-8), converted.CreatedAt);
        Assert.Equal(paid.AddHours(-8), converted.PaidAt);
        Assert.Equal(refunded.AddHours(-8), converted.RefundedAt);
        Assert.Equal(PaymentStatus.Refunded, converted.PaymentStatus);
        Assert.Equal(85.50m, converted.Amount);
        await ApplyAsync(db, new ExposedMigration().Operations(up: false));
        var restored = await db.Payments.AsNoTracking().SingleAsync(p => p.PaymentId == payment.PaymentId);
        Assert.Equal(created, restored.CreatedAt);
        Assert.Equal(paid, restored.PaidAt);
        Assert.Equal(refunded, restored.RefundedAt);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task PendingOnlineAttemptPreventsConversionWithoutChangingItsToken()
    {
        await TestDatabase.PrepareOnceAsync();
        await using var db = NewContext();
        await db.Database.OpenConnectionAsync();
        TestDatabase.CheckIsTestDatabase((SqlConnection)db.Database.GetDbConnection());
        await using var transaction = await db.Database.BeginTransactionAsync();
        var created = new DateTime(2026, 10, 31, 12, 0, 0);
        var payment = new Payment { OrderId = 1900000002, Amount = 25.80m, PaymentMethod = PaymentMethod.Card,
            PaymentStatus = PaymentStatus.Pending, CreatedAt = created, TransactionRef = "cs_test_migration_guard" };
        db.Add(payment);
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<SqlException>(() => ApplyAsync(db, new ExposedMigration().Operations(up: true)));
        Assert.Contains("Close pending online payment checkouts", error.Message);
        // THROW may roll back SQL's transaction; no conversion can have occurred before the guard.
    }

    private static AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(TestDatabase.ConnectionString).Options);
    private static async Task ApplyAsync(AppDbContext db, IReadOnlyList<MigrationOperation> operations)
    {
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var command in generator.Generate(operations, db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
    }
    private sealed class ExposedMigration : NormalizePaymentTimestampsToUtc
    {
        public IReadOnlyList<MigrationOperation> Operations(bool up)
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            if (up) base.Up(builder); else base.Down(builder);
            return builder.Operations;
        }
    }
}
