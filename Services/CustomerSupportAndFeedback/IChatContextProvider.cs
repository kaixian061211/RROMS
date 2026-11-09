namespace RROMS.Web.Services.CustomerSupportAndFeedback;

/// <summary>Collects what the chatbot is allowed to know, as plain text for the model to read.</summary>
public interface IChatContextProvider
{
    /// <summary>
    /// The menu, opening hours, and this one customer's recent orders and upcoming reservations.
    /// It never includes payment details, other customers' data, or the customer's contact details.
    /// </summary>
    Task<string> BuildAsync(int customerUserId);
}
