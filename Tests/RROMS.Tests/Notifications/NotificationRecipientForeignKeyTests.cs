using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Models.Notifications;

namespace RROMS.Tests.Notifications;

[Trait("Type", "Integration")]
public class NotificationRecipientForeignKeyTests : DatabaseTest
{
    [Fact]
    public async Task NotificationCannotReferenceAnAccountThatDoesNotExist()
    {
        await using var db = NewContext();
        const int missingId = int.MaxValue;
        Assert.False(await db.Set<UserAccount>().AnyAsync(u => u.UserId == missingId));
        db.Notifications.Add(Notice(missingId));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sql = Assert.IsType<SqlException>(error.InnerException);
        Assert.Equal(547, sql.Number);
        Assert.Contains("FK_Notification_UserAccount_UserId", sql.Message);
    }

    [Fact]
    public async Task AccountDeletionIsRestrictedWhenNotificationHistoryExists()
    {
        await using var db = NewContext();
        var account = Account();
        db.Add(account);
        await db.SaveChangesAsync();
        db.Notifications.Add(Notice(account.UserId));
        await db.SaveChangesAsync();

        // Exercise the database constraint, rather than EF's tracked-entity rule.
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Set<UserAccount>()
            .Where(u => u.UserId == account.UserId).ExecuteDeleteAsync());
        Assert.Equal(547, error.Number);
        Assert.True(await db.Set<UserAccount>().AnyAsync(u => u.UserId == account.UserId));
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == account.UserId));
    }

    [Fact]
    public async Task AccountDeactivationPreservesNotificationHistory()
    {
        await using var db = NewContext();
        var account = Account();
        db.Add(account);
        await db.SaveChangesAsync();
        var notice = Notice(account.UserId);
        db.Notifications.Add(notice);
        await db.SaveChangesAsync();
        account.IsActive = false;
        await db.SaveChangesAsync();

        Assert.False(await db.Set<UserAccount>().Where(u => u.UserId == account.UserId).Select(u => u.IsActive).SingleAsync());
        Assert.True(await db.Notifications.AnyAsync(n => n.NotificationId == notice.NotificationId && n.UserId == account.UserId));
    }

    [Fact]
    public async Task NotificationTableStillHasTenColumnsAndModelMatchesTheMigration()
    {
        await using var db = NewContext();
        var columnCount = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.columns
            WHERE [object_id] = OBJECT_ID(N'[dbo].[Notification]')
            """).SingleAsync();
        Assert.Equal(10, columnCount);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static UserAccount Account() => new()
    {
        FullName = "Recipient constraint test", Email = Guid.NewGuid().ToString("N") + "@example.com",
        Role = UserRoles.Customer, IsActive = true, PasswordHash = "not-used-for-authentication"
    };
    private static Notification Notice(int userId) => new()
    {
        UserId = userId, Type = NotificationTypes.Announcement,
        Title = "Recipient constraint test", Message = "History must reference an existing account."
    };
}
