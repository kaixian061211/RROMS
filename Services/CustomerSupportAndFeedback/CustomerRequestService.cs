using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.CustomerSupportAndFeedback;
using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Services.CustomerSupportAndFeedback;

public class CustomerRequestService : ICustomerRequestService
{
    private readonly AppDbContext _db;

    public CustomerRequestService(AppDbContext db) => _db = db;

    private DbSet<CustomerRequest> Requests => _db.Set<CustomerRequest>();
    private DbSet<Reservation> Reservations => _db.Set<Reservation>();

    public async Task<SupportResult> SubmitAsync(int customerUserId, int? reservationId, string? requestText)
    {
        var text = (requestText ?? string.Empty).Trim();
        if (text.Length == 0) return SupportResult.Failure("Please describe your request.");
        if (text.Length > 500) return SupportResult.Failure("A request must be 500 characters or fewer.");

        if (reservationId != null)
        {
            // The booking must be this customer's own
            var owns = await Reservations.AnyAsync(r =>
                r.ReservationId == reservationId && r.CustomerUserId == customerUserId);
            if (!owns) return SupportResult.Failure("Reservation not found.");
        }

        Requests.Add(new CustomerRequest
        {
            CustomerUserId = customerUserId,
            ReservationId = reservationId,
            RequestText = text
        });
        await _db.SaveChangesAsync();
        return SupportResult.Success();
    }

    public Task<List<CustomerRequest>> GetMineAsync(int customerUserId) =>
        Requests.AsNoTracking()
            .Where(r => r.CustomerUserId == customerUserId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public async Task<List<CustomerRequest>> GetForStaffAsync(int? customerUserId, string? status)
    {
        IQueryable<CustomerRequest> query = Requests.AsNoTracking();
        if (customerUserId != null) query = query.Where(r => r.CustomerUserId == customerUserId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);
        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }
}
