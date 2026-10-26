using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.CustomerSupportAndFeedback;
using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Services.CustomerSupportAndFeedback;

public class FeedbackService : IFeedbackService
{
    private readonly AppDbContext _db;

    public FeedbackService(AppDbContext db) => _db = db;

    // AppDbContext picks these up through their configuration classes, so it needs no DbSet for them.
    private DbSet<Feedback> Feedbacks => _db.Set<Feedback>();
    private DbSet<Order> Orders => _db.Set<Order>();
    private DbSet<Reservation> Reservations => _db.Set<Reservation>();

    // ---------- Customer actions ----------

    public async Task<SupportResult> SubmitAsync(
        int customerUserId, int? orderId, int? reservationId, int rating, string? comments)
    {
        comments = Clean(comments);
        var problem = Validate(rating, comments);
        if (problem != null) return SupportResult.Failure(problem);

        if ((orderId == null) == (reservationId == null))
            return SupportResult.Failure("Choose either an order or a reservation to rate.");

        if (orderId != null)
        {
            // Must be THIS customer's own order, and it must be finished
            var order = await Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerUserId == customerUserId);
            if (order == null) return SupportResult.Failure("Order not found.");
            if (order.Status != OrderStatus.Completed)
                return SupportResult.Failure("You can only rate a completed order.");
            if (await Feedbacks.AnyAsync(f => f.OrderId == orderId))
                return SupportResult.Failure("This order has already been rated.");
        }
        else
        {
            var reservation = await Reservations.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId && r.CustomerUserId == customerUserId);
            if (reservation == null) return SupportResult.Failure("Reservation not found.");
            if (reservation.Status != ReservationStatus.Completed)
                return SupportResult.Failure("You can only rate a completed reservation.");
            if (await Feedbacks.AnyAsync(f => f.ReservationId == reservationId))
                return SupportResult.Failure("This reservation has already been rated.");
        }

        Feedbacks.Add(new Feedback
        {
            CustomerUserId = customerUserId,
            OrderId = orderId,
            ReservationId = reservationId,
            Rating = rating,
            Comments = comments
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The unique index caught a double submit that slipped past the check above
            return SupportResult.Failure("Your feedback could not be saved. It may already have been submitted.");
        }

        return SupportResult.Success();
    }

    public async Task<SupportResult> UpdateOwnAsync(int customerUserId, int feedbackId, int rating, string? comments)
    {
        comments = Clean(comments);
        var problem = Validate(rating, comments);
        if (problem != null) return SupportResult.Failure(problem);

        // The ownership check is part of the query itself
        var feedback = await Feedbacks.FirstOrDefaultAsync(f =>
            f.FeedbackId == feedbackId && f.CustomerUserId == customerUserId && !f.IsHidden);
        if (feedback == null) return SupportResult.Failure("Feedback not found.");

        feedback.Rating = rating;
        feedback.Comments = comments;
        await _db.SaveChangesAsync();
        return SupportResult.Success();
    }

    public async Task<SupportResult> DeleteOwnAsync(int customerUserId, int feedbackId)
    {
        var feedback = await Feedbacks.FirstOrDefaultAsync(f =>
            f.FeedbackId == feedbackId && f.CustomerUserId == customerUserId);
        if (feedback == null) return SupportResult.Failure("Feedback not found.");

        Feedbacks.Remove(feedback);   // a customer deleting their own feedback is a real delete
        await _db.SaveChangesAsync();
        return SupportResult.Success();
    }

    public async Task<List<RatableItem>> GetRatableAsync(int customerUserId)
    {
        var ratedOrderIds = await Feedbacks
            .Where(f => f.CustomerUserId == customerUserId && f.OrderId != null)
            .Select(f => f.OrderId.GetValueOrDefault())
            .ToListAsync();
        var ratedReservationIds = await Feedbacks
            .Where(f => f.CustomerUserId == customerUserId && f.ReservationId != null)
            .Select(f => f.ReservationId.GetValueOrDefault())
            .ToListAsync();

        var orders = await Orders.AsNoTracking()
            .Where(o => o.CustomerUserId == customerUserId
                        && o.Status == OrderStatus.Completed
                        && !ratedOrderIds.Contains(o.OrderId))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var reservations = await Reservations.AsNoTracking()
            .Where(r => r.CustomerUserId == customerUserId
                        && r.Status == ReservationStatus.Completed
                        && !ratedReservationIds.Contains(r.ReservationId))
            .OrderByDescending(r => r.ReservationDate)
            .ToListAsync();

        var items = new List<RatableItem>();
        items.AddRange(orders.Select(o =>
            new RatableItem("Order", o.OrderId, $"Order #{o.OrderId} on {o.CreatedAt:dd MMM yyyy}")));
        items.AddRange(reservations.Select(r =>
            new RatableItem("Reservation", r.ReservationId,
                $"Reservation on {r.ReservationDate:dd MMM yyyy} at {r.TimeSlot:HH:mm}")));
        return items;
    }

    public async Task<List<FeedbackView>> GetMineAsync(int customerUserId)
    {
        var rows = await LoadAsync(stars: null, includeHidden: false, customerUserId);
        return rows.Select(f => ToView(f, "You")).ToList();
    }

    // ---------- Lists for each role ----------

    public async Task<List<FeedbackView>> GetVisibleAsync(int? stars)
    {
        var rows = await LoadAsync(stars, includeHidden: false);
        // TODO: show the customer's first name once the login module has a user table
        return rows.Select(f => ToView(f, "Customer")).ToList();
    }

    public async Task<List<FeedbackView>> GetForStaffAsync(int? stars)
    {
        var rows = await LoadAsync(stars, includeHidden: false);
        return rows.Select(f => ToView(f, $"Customer #{f.CustomerUserId}")).ToList();
    }

    public async Task<List<FeedbackView>> GetAllForAdminAsync(int? stars)
    {
        var rows = await LoadAsync(stars, includeHidden: true);
        return rows.Select(f => ToView(f, $"Customer #{f.CustomerUserId}")).ToList();
    }

    // ---------- Admin actions (the page must be restricted to admins) ----------

    public async Task<SupportResult> ReplyAsync(int feedbackId, string reply)
    {
        reply = (reply ?? string.Empty).Trim();
        if (reply.Length == 0) return SupportResult.Failure("The reply cannot be empty.");
        if (reply.Length > 1000) return SupportResult.Failure("A reply must be 1000 characters or fewer.");

        var feedback = await Feedbacks.FindAsync(feedbackId);
        if (feedback == null) return SupportResult.Failure("Feedback not found.");

        feedback.AdminReply = reply;
        feedback.RepliedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return SupportResult.Success();
    }

    public async Task<SupportResult> SetHiddenAsync(int feedbackId, bool hidden)
    {
        var feedback = await Feedbacks.FindAsync(feedbackId);
        if (feedback == null) return SupportResult.Failure("Feedback not found.");

        feedback.IsHidden = hidden;   // soft delete, or restore
        await _db.SaveChangesAsync();
        return SupportResult.Success();
    }

    // ---------- Helpers ----------

    private async Task<List<Feedback>> LoadAsync(int? stars, bool includeHidden, int? customerUserId = null)
    {
        IQueryable<Feedback> query = Feedbacks.AsNoTracking();
        if (!includeHidden) query = query.Where(f => !f.IsHidden);
        if (stars != null) query = query.Where(f => f.Rating == stars);
        if (customerUserId != null) query = query.Where(f => f.CustomerUserId == customerUserId);
        return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
    }

    private static string? Clean(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string? Validate(int rating, string? comments)
    {
        if (rating < 1 || rating > 5) return "The rating must be between 1 and 5.";
        if (comments != null && comments.Length > 1000) return "A comment must be 1000 characters or fewer.";
        return null;
    }

    private static FeedbackView ToView(Feedback f, string displayName) =>
        new(f.FeedbackId, displayName, f.Rating, f.Comments, f.CreatedAt,
            f.AdminReply, f.RepliedAt, f.IsHidden, f.OrderId, f.ReservationId);
}
