using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Restaurant;

namespace RROMS.Tests;

/// <summary>
/// The tests' own database, RROMS_Tests on the local SQL Server. Every way into it goes through
/// CheckIsTestDatabase, so the tests refuse to run, and never empty a table, anywhere else.
/// </summary>
public static class TestDatabase
{
    public const string Name = "RROMS_Tests";

    private const string DefaultConnectionString =
        "Server=localhost;Database=RROMS_Tests;Trusted_Connection=True;TrustServerCertificate=True";

    private static readonly Lazy<Task> Prepared = new(PrepareAsync);

    /// <summary>Set RROMS_TEST_CONNECTION to use another server. The database must still be RROMS_Tests.</summary>
    public static string ConnectionString =>
        CheckIsTestDatabase(Environment.GetEnvironmentVariable("RROMS_TEST_CONNECTION") ?? DefaultConnectionString);

    /// <summary>Throws unless the connection string names exactly the RROMS_Tests database.</summary>
    public static string CheckIsTestDatabase(string connectionString)
    {
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (database != Name)
            throw new InvalidOperationException(
                $"The tests only run against the {Name} database, but the connection string names '{database}'. Nothing was changed.");
        return connectionString;
    }

    /// <summary>The same check on an open connection, so it's the database the server actually put us in.</summary>
    public static void CheckIsTestDatabase(SqlConnection connection)
    {
        if (connection.Database != Name)
            throw new InvalidOperationException(
                $"The tests only run against the {Name} database, but the connection is using '{connection.Database}'. Nothing was changed.");
    }

    /// <summary>Runs PrepareAsync the first time any test asks, and only once per test run.</summary>
    public static Task PrepareOnceAsync() => Prepared.Value;

    /// <summary>
    /// Brings RROMS_Tests up to the latest migration (creating it the first time), empties it, makes sure the
    /// tables exist, and clears any opening hours or settings, so the restaurant counts as open.
    /// </summary>
    private static async Task PrepareAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        await EmptyAsync(db);
        await db.Set<OperatingHour>().ExecuteDeleteAsync();
        await db.Set<RestaurantInfo>().ExecuteDeleteAsync();
        await AddTablesOnceAsync(db);
    }

    /// <summary>
    /// Tables 1 to 10 in one dining area, "Main hall". Tables 1 to 9 seat 8 and table 10 seats 2. They're saved
    /// the first time and never deleted, so their ids stay 1 to 10, the same as their numbers, as the tests use them.
    /// </summary>
    private static async Task AddTablesOnceAsync(AppDbContext db)
    {
        if (!await db.Set<RestaurantTable>().AnyAsync())
        {
            var area = new DiningArea { Name = "Main hall" };
            for (var number = 1; number <= 10; number++)
            {
                db.Add(new RestaurantTable { TableNumber = number.ToString(), DiningArea = area, Capacity = number == 10 ? 2 : 8 });
                await db.SaveChangesAsync();   // one at a time, so the ids come out in order
            }
        }

        var tables = await db.Set<RestaurantTable>().AsNoTracking().ToListAsync();
        if (tables.Count != 10 || tables.Any(t => t.TableNumber != t.RestaurantTableId.ToString() || !t.IsActive))
            throw new InvalidOperationException(
                $"{Name} should have exactly tables 1 to 10, with ids 1 to 10. Drop the {Name} database and run the tests again to recreate it.");
    }

    /// <summary>Deletes every order, payment, reservation and menu item. Checks the database first, every time.</summary>
    public static async Task EmptyAsync(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        CheckIsTestDatabase((SqlConnection)db.Database.GetDbConnection());

        await db.Set<OrderItem>().ExecuteDeleteAsync();
        await db.Set<Order>().ExecuteDeleteAsync();
        await db.Payments.ExecuteDeleteAsync();
        await db.Set<Reservation>().ExecuteDeleteAsync();
        await db.Set<MenuItem>().ExecuteDeleteAsync();
        await db.Set<MenuCategory>().ExecuteDeleteAsync();
    }
}
