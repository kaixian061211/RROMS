namespace RROMS.Web.Models.CustomerSupportAndFeedback;

/// <summary>Values stored in CustomerRequest.Status. They must match the CHECK constraint on the table.</summary>
public static class RequestStatus
{
    public const string Pending = "pending";
    public const string Acknowledged = "acknowledged";
    public const string Completed = "completed";
    public const string Declined = "declined";

    public static readonly string[] All = { Pending, Acknowledged, Completed, Declined };
}
