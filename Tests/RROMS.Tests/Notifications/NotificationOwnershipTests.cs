using RROMS.Web.Models.Notifications;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Notifications;

namespace RROMS.Tests.Notifications;

[Trait("Type", "Integration")]
public class NotificationOwnershipTests : DatabaseTest
{
    [Fact]
    public async Task StagingChecksAllRelatedRecordOwnersAndSavesTrimmedContent()
    {
        await using var db = NewContext();
        var order = new Order { CustomerUserId = Alice, TableId = 1 };
        var reservation = new Reservation { CustomerUserId = Alice, TableId = 1, ReservationDate = Tomorrow,
            TimeSlot = Lunch, PartySize = 2 };
        db.AddRange(order, reservation);
        await db.SaveChangesAsync();
        var payment = new Payment { OrderId = order.OrderId, Amount = 10, PaymentMethod = PaymentMethod.Card };
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = new NotificationService(db, new NoopRealtime());
        foreach (var input in new[]
        {
            new NotificationInput { Type = NotificationTypes.Order, OrderId = order.OrderId },
            new NotificationInput { Type = NotificationTypes.Reservation, ReservationId = reservation.ReservationId },
            new NotificationInput { Type = NotificationTypes.Payment, PaymentId = payment.PaymentId }
        })
        {
            input.UserId = Bob; input.Title = " Ready "; input.Message = " Status changed. ";
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.StageAsync(input));
            input.UserId = Alice;
            var notice = await service.StageAsync(input);
            await db.SaveChangesAsync();
            Assert.Equal("Ready", notice.Title);
            Assert.Equal("Status changed.", notice.Message);
            Assert.False(notice.IsRead);
            Assert.NotEqual(default, notice.CreatedAt);
        }
        await Assert.ThrowsAsync<ArgumentException>(() => service.StageAsync(new NotificationInput
        {
            UserId = Alice, Type = NotificationTypes.Payment, OrderId = order.OrderId, Title = "Invalid", Message = "Wrong reference."
        }));
    }

    private sealed class NoopRealtime : INotificationRealtime
    {
        public Task PublishAsync(Notification notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task PublishReadStateChangedAsync(int userId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
