namespace RROMS.Web.Services.CustomerSupportAndFeedback;

/// <summary>
/// The one place the chatbot talks to an AI model. Swapping provider means writing one new class
/// that implements this and registering it in Program.cs. Nothing else changes.
/// </summary>
public interface IChatModelClient
{
    /// <summary>
    /// Sends the instructions, the conversation so far and the new message, and returns the model's reply.
    /// Throws when the model can't be reached or isn't set up. The chatbot service turns that into a friendly reply.
    /// </summary>
    Task<string> GetReplyAsync(
        string systemPrompt,
        IReadOnlyList<(string Sender, string Text)> history,
        string userMessage);
}
