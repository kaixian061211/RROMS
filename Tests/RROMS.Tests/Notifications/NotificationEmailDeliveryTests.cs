using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using RROMS.Web.Data;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Services.Notifications;

namespace RROMS.Tests.Notifications;

// Uses guarded, committed test rows because delivery must run after commit.
// All provider calls are recorded locally; no real email or push is sent.
[Trait("Type", "Integration")]
public class NotificationEmailDeliveryTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDatabase.PrepareOnceAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public Task MissingRecipientResolvesTheOwnersAccountAndDeliversEachChannelOnce() => WithNoticeAsync(async (db, owner, notice) =>
    {
        var channels = new RecordingChannels();
        await Delivery(db, channels).DeliverAsync(notice.NotificationId, sendEmail: true);
        Assert.Equal(owner.Email, Assert.Single(channels.EmailAddresses));
        Assert.Equal(1, channels.RealtimeCalls);
        Assert.Equal(1, channels.PushCalls);
    });

    [Theory]
    [InlineData("Customer", false)]
    [InlineData("Staff", true)]
    [InlineData("Admin", true)]
    public Task AccountLookupExcludesInactiveAndNonCustomerAccounts(string role, bool active) => WithNoticeAsync(async (db, _, notice) =>
    {
        var channels = new RecordingChannels();
        await Delivery(db, channels).DeliverAsync(notice.NotificationId, sendEmail: true);
        Assert.Empty(channels.EmailAddresses);
        Assert.Equal(1, channels.RealtimeCalls);
        Assert.Equal(1, channels.PushCalls);
    }, role, active);

    [Theory]
    [InlineData(true, "other@example.com")]
    [InlineData(false, "local-name-only")]
    [InlineData(false, "")]
    public Task MismatchedOrInvalidExplicitRecipientDoesNotFallBackToAnotherAddress(bool mismatch, string email) => WithNoticeAsync(async (db, owner, notice) =>
    {
        var channels = new RecordingChannels();
        await Delivery(db, channels).DeliverAsync(notice.NotificationId, sendEmail: true,
            recipient: new NotificationEmailRecipient(mismatch ? owner.UserId + 1 : owner.UserId, email));
        Assert.Empty(channels.EmailAddresses);
        Assert.Equal(1, channels.RealtimeCalls);
        Assert.Equal(1, channels.PushCalls);
    });

    [Fact]
    public Task AccountLookupFailureDoesNotSuppressRealtimeOrPush() => WithNoticeAsync(async (_, _, notice) =>
    {
        var interceptor = new FailAccountLookup();
        await using var db = Context(interceptor);
        var channels = new RecordingChannels();
        await Delivery(db, channels).DeliverAsync(notice.NotificationId, sendEmail: true);
        Assert.True(interceptor.WasCalled);
        Assert.Empty(channels.EmailAddresses);
        Assert.Equal(1, channels.RealtimeCalls);
        Assert.Equal(1, channels.PushCalls);
    });

    [Fact]
    public Task DisabledEmailDoesNotReadAccounts() => WithNoticeAsync(async (_, _, notice) =>
    {
        var interceptor = new FailAccountLookup();
        await using var db = Context(interceptor);
        var channels = new RecordingChannels();
        await Delivery(db, channels).DeliverAsync(notice.NotificationId);
        Assert.False(interceptor.WasCalled);
        Assert.Empty(channels.EmailAddresses);
        Assert.Equal(1, channels.RealtimeCalls);
        Assert.Equal(1, channels.PushCalls);
    });

    [Fact]
    public Task EmailSendFailureDoesNotSuppressRealtimeOrPush() => WithNoticeAsync(async (db, _, notice) =>
    {
        var channels = new RecordingChannels { FailEmailSend = true };
        await Delivery(db, channels).DeliverAsync(notice.NotificationId, sendEmail: true);
        Assert.Equal(1, channels.EmailAttempts);
        Assert.Empty(channels.EmailAddresses);
        Assert.Equal(1, channels.RealtimeCalls);
        Assert.Equal(1, channels.PushCalls);
    });

    private static AppDbContext Context(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(TestDatabase.ConnectionString)
            .AddInterceptors(interceptors).Options);

    private static NotificationDelivery Delivery(AppDbContext db, RecordingChannels channels) => new(
        db, channels, channels, channels, NullLogger<NotificationDelivery>.Instance);

    private static async Task WithNoticeAsync(Func<AppDbContext, UserAccount, Notification, Task> check,
        string role = UserRoles.Customer, bool active = true)
    {
        await using var db = Context();
        var owner = new UserAccount
        {
            FullName = "Email lookup test", Email = Guid.NewGuid().ToString("N") + "@example.com",
            Role = role, IsActive = active, PasswordHash = "not-used-for-authentication"
        };
        db.Add(owner);
        await db.SaveChangesAsync();
        try
        {
            var notice = new Notification
            {
                UserId = owner.UserId, Type = NotificationTypes.Announcement,
                Title = "Email test", Message = "Test delivery."
            };
            db.Add(notice);
            await db.SaveChangesAsync();
            await check(db, owner, notice);
        }
        finally
        {
            await db.Notifications.Where(n => n.UserId == owner.UserId).ExecuteDeleteAsync();
            await db.Set<UserAccount>().Where(u => u.UserId == owner.UserId).ExecuteDeleteAsync();
        }
    }

    private sealed class RecordingChannels : INotificationRealtime, INotificationPushSender, INotificationEmailSender
    {
        public int RealtimeCalls { get; private set; }
        public int PushCalls { get; private set; }
        public int EmailAttempts { get; private set; }
        public bool FailEmailSend { get; init; }
        public List<string> EmailAddresses { get; } = [];
        public Task PublishAsync(Notification notification, CancellationToken ct = default)
        { RealtimeCalls++; return Task.CompletedTask; }
        public Task PublishReadStateChangedAsync(int userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendAsync(Notification notification, CancellationToken ct = default)
        { PushCalls++; return Task.CompletedTask; }
        public Task SendAsync(MailboxAddress recipient, string subject, string message, CancellationToken ct = default)
        {
            EmailAttempts++;
            if (FailEmailSend) throw new InvalidOperationException("Simulated email provider failure.");
            EmailAddresses.Add(recipient.Address);
            return Task.CompletedTask;
        }
    }

    private sealed class FailAccountLookup : DbCommandInterceptor
    {
        public bool WasCalled { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken ct = default)
        {
            if (command.CommandText.Contains("[UserAccount]", StringComparison.OrdinalIgnoreCase))
            {
                WasCalled = true;
                throw new InvalidOperationException("Simulated account lookup failure.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
