using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Models.Restaurant;
using RROMS.Web.Services.Notifications;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Pages.Restaurant.Manage;

namespace RROMS.Tests.Notifications;

[Trait("Type", "Integration")]
public class NotificationPublishingTests : DatabaseTest
{
    [Fact]
    public async Task PromotionTargetsOnlyActiveCustomersWithOrderOrReservationHistoryOnce()
    {
        await using var db = NewContext();
        var customer = Account(UserRoles.Customer);
        var orderCustomer = Account(UserRoles.Customer);
        var noHistory = Account(UserRoles.Customer);
        var staff = Account(UserRoles.Staff);
        var inactive = Account(UserRoles.Customer, active: false);
        var promotion = new Promotion
        {
            Title = "Lunch deal", Description = "Save on lunch.", DiscountValue = 10,
            StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3))
        };
        db.AddRange(customer, orderCustomer, noHistory, staff, inactive, promotion);
        await db.SaveChangesAsync();
        await AddReservationAsync(customer.UserId, 1, DateOnly.FromDateTime(DateTime.Today.AddDays(3)), new TimeOnly(12, 0));
        await AddReservationAsync(staff.UserId, 2, DateOnly.FromDateTime(DateTime.Today.AddDays(3)), new TimeOnly(12, 0));
        await AddReservationAsync(inactive.UserId, 3, DateOnly.FromDateTime(DateTime.Today.AddDays(3)), new TimeOnly(12, 0));
        db.AddRange(new Order { CustomerUserId = orderCustomer.UserId, TableId = 1 },
            new Order { CustomerUserId = orderCustomer.UserId, TableId = 1 });
        await db.SaveChangesAsync();
        var delivery = new RecordingDelivery();
        using var services = new ServiceCollection().AddSingleton<INotificationDelivery>(delivery).BuildServiceProvider();
        await Publisher(db, services).PromotionPublishedAsync(promotion);

        var notices = await db.Notifications.AsNoTracking().Where(n => n.Title == "New promotion: Lunch deal").ToListAsync();
        var ownNotice = Assert.Single(notices, n => n.UserId == customer.UserId);
        Assert.Equal(NotificationTypes.Promotion, ownNotice.Type);
        Assert.Contains("10% off", ownNotice.Message);
        var orderNotice = Assert.Single(notices, n => n.UserId == orderCustomer.UserId);
        Assert.DoesNotContain(notices, n => n.UserId == staff.UserId || n.UserId == inactive.UserId || n.UserId == noHistory.UserId);
        Assert.Single(delivery.Calls, c => c.Id == ownNotice.NotificationId);
        Assert.Single(delivery.Calls, c => c.Id == orderNotice.NotificationId);
        Assert.All(delivery.Calls, call => Assert.False(call.SendEmail));
    }

    [Fact]
    public async Task AnnouncementUsesAShortNotificationWithoutChangingItsFullText()
    {
        await using var db = NewContext();
        var customer = Account(UserRoles.Customer);
        var announcement = new Announcement { Title = new string('T', 150), Message = new string('M', 1000) };
        db.AddRange(customer, announcement);
        await db.SaveChangesAsync();
        var delivery = new RecordingDelivery();
        using var services = new ServiceCollection().AddSingleton<INotificationDelivery>(delivery).BuildServiceProvider();
        await Publisher(db, services).AnnouncementPublishedAsync(announcement);

        var notice = await db.Notifications.AsNoTracking().SingleAsync(n => n.UserId == customer.UserId);
        Assert.Equal(100, notice.Title.Length);
        Assert.Equal(500, notice.Message.Length);
        Assert.Equal(1000, announcement.Message.Length);
        Assert.Single(delivery.Calls, c => c.Id == notice.NotificationId);
    }

    [Fact]
    public async Task EditingALiveAnnouncementDoesNotBroadcastAgainButReactivationDoes()
    {
        await using var db = NewContext();
        db.Add(Account(UserRoles.Customer));
        await db.SaveChangesAsync();
        var delivery = new RecordingDelivery();
        using var services = new ServiceCollection().AddSingleton<INotificationDelivery>(delivery).BuildServiceProvider();
        var page = new AnnouncementsModel(db, Publisher(db, services))
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider()),
            Input = new AnnouncementsModel.InputModel { Title = "Opening hours", Message = "Open as usual.", IsActive = true }
        };
        await page.OnPostSaveAsync();
        var firstCount = delivery.Calls.Count;
        Assert.True(firstCount > 0);
        page.Input.AnnouncementId = (await db.Set<Announcement>().SingleAsync(a => a.Title == "Opening hours")).AnnouncementId;
        page.Input.Message = "Updated wording.";
        await page.OnPostSaveAsync();
        Assert.Equal(firstCount, delivery.Calls.Count);
        page.Input.IsActive = false;
        await page.OnPostSaveAsync();
        Assert.Equal(firstCount, delivery.Calls.Count);
        page.Input.IsActive = true;
        await page.OnPostSaveAsync();
        Assert.Equal(firstCount * 2, delivery.Calls.Count);
    }

    [Theory]
    [InlineData(ReservationStatus.Confirmed, true)]
    [InlineData(ReservationStatus.Cancelled, false)]
    public async Task ReservationStatusChangeUsesOneDeliveryAndEmailsOnlyConfirmation(string status, bool expectedEmail)
    {
        var reservation = await AddReservationAsync(1, 1, DateOnly.FromDateTime(DateTime.Today.AddDays(3)), new TimeOnly(12, 0));
        await using var db = NewContext();
        var delivery = new RecordingDelivery();
        var service = new ReservationService(db, new RestaurantLookup(db),
            new NotificationService(db, new NoopRealtime()), delivery, NullLogger<ReservationService>.Instance);
        var result = await service.ChangeStatusAsync(reservation.ReservationId, status, reservation.RowVersion);
        Assert.True(result.Succeeded, result.ErrorMessage);
        var notice = await db.Notifications.AsNoTracking().SingleAsync(n => n.ReservationId == reservation.ReservationId);
        var sent = Assert.Single(delivery.Calls);
        Assert.Equal(notice.NotificationId, sent.Id);
        Assert.Equal(expectedEmail, sent.SendEmail);
    }

    [Fact]
    public async Task RequestedEmailUsesTheNotificationOwnersStoredAccountAddress()
    {
        // Delivery must run outside a transaction. Use this test's own committed
        // rows and remove only those rows afterward, not the fixture transaction.
        await using var db = new RROMS.Web.Data.AppDbContext(
            new DbContextOptionsBuilder<RROMS.Web.Data.AppDbContext>()
                .UseSqlServer(TestDatabase.ConnectionString).Options);
        var customer = Account(UserRoles.Customer);
        db.Add(customer);
        await db.SaveChangesAsync();
        try
        {
            var notice = new Notification
            {
                UserId = customer.UserId, Type = NotificationTypes.Announcement,
                Title = "Confirmation", Message = "Your reservation is confirmed."
            };
            db.Add(notice);
            await db.SaveChangesAsync();
            var email = new RecordingEmail();
            var delivery = new NotificationDelivery(db, new NoopRealtime(), email, new NoopPush(),
                NullLogger<NotificationDelivery>.Instance);
            await delivery.DeliverAsync(notice.NotificationId, sendEmail: true);
            Assert.Equal(customer.Email, Assert.Single(email.Addresses));
        }
        finally
        {
            await db.Notifications.Where(n => n.UserId == customer.UserId).ExecuteDeleteAsync();
            await db.Set<UserAccount>().Where(u => u.UserId == customer.UserId).ExecuteDeleteAsync();
        }
    }

    private static UserAccount Account(string role, bool active = true) => new()
    {
        FullName = "Notification test", Email = Guid.NewGuid().ToString("N") + "@example.com",
        Role = role, IsActive = active, PasswordHash = "not-used-for-authentication"
    };

    private static NotificationPublisher Publisher(RROMS.Web.Data.AppDbContext db, ServiceProvider services) => new(
        db, new NotificationService(db, new NoopRealtime()), services.GetRequiredService<IServiceScopeFactory>(),
        NullLogger<NotificationPublisher>.Instance);

    private sealed class RecordingDelivery : INotificationDelivery
    {
        public ConcurrentBag<(int Id, bool SendEmail)> Calls { get; } = [];
        public Task DeliverAsync(int notificationId, bool sendEmail = false,
            NotificationEmailRecipient? recipient = null, CancellationToken ct = default)
        {
            Calls.Add((notificationId, sendEmail));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEmail : INotificationEmailSender
    {
        public List<string> Addresses { get; } = [];
        public Task SendAsync(MailboxAddress recipient, string subject, string message, CancellationToken ct = default)
        {
            Addresses.Add(recipient.Address);
            return Task.CompletedTask;
        }
    }
    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private sealed class NoopRealtime : INotificationRealtime
    {
        public Task PublishAsync(Notification notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task PublishReadStateChangedAsync(int userId, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class NoopPush : INotificationPushSender
    {
        public Task SendAsync(Notification notification, CancellationToken ct = default) => Task.CompletedTask;
    }
}
