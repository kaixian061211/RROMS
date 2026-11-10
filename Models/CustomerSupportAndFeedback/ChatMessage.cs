using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.CustomerSupportAndFeedback;

public class ChatMessage
{
    public int ChatMessageId { get; set; }

    public int ChatSessionId { get; set; }
    public ChatSession ChatSession { get; set; } = null!;

    [Required, StringLength(20)]
    public string Sender { get; set; } = ChatSender.Customer;

    [Required, StringLength(2000)]
    public string Text { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }
}
