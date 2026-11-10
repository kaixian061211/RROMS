using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RROMS.Web.Data;
using RROMS.Tests.Integration;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;

namespace RROMS.Tests.Payments;

/// <summary>Opt-in, local browser fixture. Uses RROMS_Tests and fake Stripe/email delivery.</summary>
[Trait("Type", "Manual")]
public class PaymentBrowserPreview
{
    [Fact]
    public async Task ServeIsolatedPaymentPagesForBrowserReview()
    {
        // Explicit opt-in prevents opening ports or holding normal regression runs open.
        if (Environment.GetEnvironmentVariable("RROMS_PAYMENT_PREVIEW") != "1") return;
        var stateFile = Environment.GetEnvironmentVariable("RROMS_PAYMENT_PREVIEW_FILE")
            ?? throw new InvalidOperationException("Set RROMS_PAYMENT_PREVIEW_FILE to an output path.");
        await TestDatabase.PrepareOnceAsync();
        await using var app = new PaymentWebApp();
        app.UseKestrel(5198);
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await TestDatabase.EmptyAsync(db);
        var menu = await TestMenu.SaveToDatabaseAsync(db);
        var orders = new List<Order>();
        foreach (var amount in new[] { 25.80m, 37.20m, 14.20m, 85.50m, 12.90m, 18.40m })
        {
            var order = new Order { CustomerUserId = TestUsers.CustomerA.Id, TableId = 4,
                Items = { new OrderItem { MenuItemId = menu["Nasi Lemak"], Quantity = 1, UnitPrice = amount } } };
            db.Add(order);
            orders.Add(order);
        }
        await db.SaveChangesAsync();
        var now = DateTime.UtcNow;
        db.AddRange(
            new Payment { OrderId = orders[0].OrderId, Amount = 25.80m, PaymentMethod = PaymentMethod.PayAtCounter, PaymentStatus = PaymentStatus.Pending, CreatedAt = now.AddMinutes(-12) },
            new Payment { OrderId = orders[1].OrderId, Amount = 37.20m, PaymentMethod = PaymentMethod.Cash, PaymentStatus = PaymentStatus.Paid, RecordedByStaffId = TestUsers.Staff.Id, CreatedAt = now.AddHours(-1), PaidAt = now.AddMinutes(-45) },
            new Payment { OrderId = orders[2].OrderId, Amount = 14.20m, PaymentMethod = PaymentMethod.Card, PaymentStatus = PaymentStatus.Pending, CreatedAt = now.AddMinutes(-3), TransactionRef = "cs_test_preview_pending" },
            new Payment { OrderId = orders[3].OrderId, Amount = 85.50m, PaymentMethod = PaymentMethod.Card, PaymentStatus = PaymentStatus.Refunded, CreatedAt = now.AddDays(-1), PaidAt = now.AddDays(-1).AddMinutes(2), RefundedAt = now.AddMinutes(-20), RefundedByStaffId = TestUsers.Staff.Id, RefundReason = "Customer cancelled before preparation", TransactionRef = "cs_test_preview_refunded", StripePaymentIntentId = "pi_test_preview_refunded" },
            new Payment { OrderId = orders[5].OrderId, Amount = 18.40m, PaymentMethod = PaymentMethod.OnlineBanking, PaymentStatus = PaymentStatus.Failed, CreatedAt = now.AddMinutes(-8), TransactionRef = "cs_test_preview_failed" });
        await db.SaveChangesAsync();
        var payments = await db.Payments.AsNoTracking().OrderBy(p => p.PaymentId).ToListAsync();
        await File.WriteAllTextAsync(stateFile, JsonSerializer.Serialize(new { url = "http://localhost:5198", orderIds = orders.Select(o => o.OrderId), paymentIds = payments.Select(p => p.PaymentId) }));
        try
        {
            var deadline = DateTime.UtcNow.AddMinutes(20);
            while (!File.Exists(stateFile + ".stop") && DateTime.UtcNow < deadline)
                await Task.Delay(500);
        }
        finally
        {
            await TestDatabase.EmptyAsync(db);
        }
    }
}
