using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RROMS.Web.Data;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Tests;

/// <summary>
/// Base class for tests that use the database. xUnit creates a new instance for every test, so each test
/// gets its own transaction, and everything it saves is rolled back when it finishes.
/// </summary>
public abstract class DatabaseTest : IAsyncLifetime
{
    // Dates are worked out from the day the tests run, so they never go out of date.
    protected static DateOnly Tomorrow => DateOnly.FromDateTime(DateTime.Today.AddDays(1));
    protected static DateOnly Yesterday => DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
    protected static readonly TimeOnly Lunch = new(12, 0);
    protected static readonly TimeOnly Dinner = new(19, 30);

    protected const int Alice = 9001;
    protected const int Bob = 9002;

    private SqlConnection _connection = null!;
    private SqlTransaction _transaction = null!;
    private DbContextOptions<AppDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await TestDatabase.PrepareOnceAsync();

        _connection = new SqlConnection(TestDatabase.ConnectionString);
        await _connection.OpenAsync();
        TestDatabase.CheckIsTestDatabase(_connection);
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_connection).Options;
    }

    public async Task DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// A fresh DbContext, like a new web request, inside this test's transaction. An interceptor lets a test
    /// step in while the context saves.
    /// </summary>
    protected AppDbContext NewContext(params IInterceptor[] interceptors)
    {
        var options = interceptors.Length == 0
            ? _options
            : new DbContextOptionsBuilder<AppDbContext>(_options).AddInterceptors(interceptors).Options;
        var db = new AppDbContext(options);
        db.Database.UseTransaction(_transaction);
        return db;
    }

    protected ReservationService NewReservationService(params IInterceptor[] interceptors) => new(NewContext(interceptors));

    /// <summary>
    /// Saves a reservation straight to the database, skipping the service's checks, so a test can start
    /// from a booking the service wouldn't create, such as one in the past or one already completed.
    /// </summary>
    protected async Task<Reservation> AddReservationAsync(
        int customer, int table, DateOnly date, TimeOnly time, string status = ReservationStatus.Pending)
    {
        var reservation = new Reservation
        {
            CustomerUserId = customer, TableId = table, ReservationDate = date, TimeSlot = time, PartySize = 2, Status = status
        };
        var db = NewContext();
        db.Add(reservation);
        await db.SaveChangesAsync();
        return reservation;
    }

    /// <summary>The reservation as it is in the database now.</summary>
    protected Task<Reservation> ReloadReservationAsync(int reservationId) =>
        NewContext().Set<Reservation>().AsNoTracking().SingleAsync(r => r.ReservationId == reservationId);

    /// <summary>The real order service, with the temporary menu unless the test passes its own.</summary>
    protected OrderService NewOrderService(IMenuItemLookup? menu = null)
    {
        var db = NewContext();
        return new OrderService(db, menu ?? new TemporaryMenuItemLookup(), new PaymentQueries(db, new PaymentOrderProvider(db)));
    }
}
