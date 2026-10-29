namespace RROMS.Web.Models.CustomerSupportAndFeedback;

/// <summary>One message in a conversation, as the chat page shows it. Sender is "Customer" or "Bot".</summary>
public record ChatTurn(string Sender, string Text, DateTime SentAt);
