using RROMS.Web.Models.CustomerSupportAndFeedback;

namespace RROMS.Web.Services.CustomerSupportAndFeedback;

public interface ICustomerRequestService
{
    Task<SupportResult> SubmitAsync(int customerUserId, int? reservationId, string? requestText);
    Task<List<CustomerRequest>> GetMineAsync(int customerUserId);

    /// <summary>Staff view, newest first. Optionally one customer's requests, or only one status.</summary>
    Task<List<CustomerRequest>> GetForStaffAsync(int? customerUserId, string? status);
}
