namespace RROMS.Web.Models.CustomerSupportAndFeedback;

public class ChatSession
{
    public int ChatSessionId { get; set; }

    /// <summary>No FK yet: the user table belongs to the login module.</summary>
    public int CustomerUserId { get; set; }

    public DateTime StartedAt { get; set; }

    public List<ChatMessage> Messages { get; set; } = new();
}
