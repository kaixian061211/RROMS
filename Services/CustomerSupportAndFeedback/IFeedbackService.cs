using RROMS.Web.Models.CustomerSupportAndFeedback;

namespace RROMS.Web.Services.CustomerSupportAndFeedback;

public interface IFeedbackService
{
    // Customer actions (the page passes the signed-in customer's id, never an id typed into a form)
    Task<SupportResult> SubmitAsync(int customerUserId, int? orderId, int? reservationId, int rating, string? comments);
    Task<SupportResult> UpdateOwnAsync(int customerUserId, int feedbackId, int rating, string? comments);
    Task<SupportResult> DeleteOwnAsync(int customerUserId, int feedbackId);
    Task<List<RatableItem>> GetRatableAsync(int customerUserId);
    Task<List<FeedbackView>> GetMineAsync(int customerUserId);

    /// <summary>Everyone's visible feedback, newest first, optionally one star rating. No hidden items.</summary>
    Task<List<FeedbackView>> GetVisibleAsync(int? stars);

    // Staff (read-only) and admin
    Task<List<FeedbackView>> GetForStaffAsync(int? stars);
    Task<List<FeedbackView>> GetAllForAdminAsync(int? stars);
    Task<SupportResult> ReplyAsync(int feedbackId, string reply);
    Task<SupportResult> SetHiddenAsync(int feedbackId, bool hidden);
}
