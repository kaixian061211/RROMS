using System.Net.Http.Json;
using System.Text.Json;
using RROMS.Web.Models.CustomerSupportAndFeedback;

namespace RROMS.Web.Services.CustomerSupportAndFeedback;

/// <summary>
/// Asks Google's Gemini API for a reply. The key and the model name come from configuration
/// (user-secrets: Gemini:ApiKey and Gemini:Model), never from the code or appsettings.json.
/// </summary>
public class GeminiChatClient : IChatModelClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public GeminiChatClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<string> GetReplyAsync(
        string systemPrompt,
        IReadOnlyList<(string Sender, string Text)> history,
        string userMessage)
    {
        // Read the settings here, not in the constructor, so a missing key only breaks the chatbot
        // and never stops the page from loading.
        var apiKey = _config["Gemini:ApiKey"];
        var model = _config["Gemini:Model"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("Gemini:ApiKey and Gemini:Model are not set.");

        // Gemini calls the bot's role "model" and the customer's role "user"
        var contents = new List<object>();
        foreach (var (sender, text) in history)
            contents.Add(new { role = sender == ChatSender.Bot ? "model" : "user", parts = new[] { new { text } } });
        contents.Add(new { role = "user", parts = new[] { new { text = userMessage } } });

        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("x-goog-api-key", apiKey);   // in a header, so it never appears in a URL or a log

        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Gemini returned status {(int)response.StatusCode}.");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // The reply is at candidates[0].content.parts[0].text. A blocked or empty answer has no such path,
        // so return "" and let the chatbot service use its fallback.
        if (doc.RootElement.TryGetProperty("candidates", out var candidates)
            && candidates.GetArrayLength() > 0
            && candidates[0].TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts)
            && parts.GetArrayLength() > 0
            && parts[0].TryGetProperty("text", out var textElement))
        {
            return textElement.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}
