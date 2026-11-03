using RROMS.Web.Models.CustomerSupportAndFeedback;

namespace RROMS.Web.Services.CustomerSupportAndFeedback;

public interface IChatbotService
{
    /// <summary>The customer's current conversation, oldest message first. Empty when they haven't chatted yet.</summary>
    Task<List<ChatTurn>> GetConversationAsync(int customerUserId);

    /// <summary>
    /// Saves the customer's question, gets an answer and saves that too. The page passes the signed-in
    /// customer's id, so a customer can only ever read their own data through the chatbot.
    /// </summary>
    Task<SupportResult> AskAsync(int customerUserId, string? message);

    /// <summary>Starts a fresh conversation. The old one stays in the database.</summary>
    Task StartNewAsync(int customerUserId);
}
