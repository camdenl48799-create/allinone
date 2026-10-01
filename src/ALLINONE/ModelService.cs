using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ALLINONE;

public sealed class ModelService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };

    public string ModelId { get; } =
        Environment.GetEnvironmentVariable("ALLINONE_MODEL") ?? "gpt-5.6-luna";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

    public async Task<string> GenerateAsync(string prompt, string? context = null, string? role = null, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return "The ALLINONE model runtime is not configured yet. Set the OPENAI_API_KEY environment variable, then restart ALLINONE. No API key is stored in the app.";

        var instructions = role switch
        {
            "code" => "You are CodeInOne, the coding and software-building model inside ALLINONE. Give practical, correct code. Never claim a file was changed unless a tool actually changed it.",
            "game" => "You are ALLINONE's game-building model. Help design games, gameplay systems, code, project structure, and safe creative assets.",
            "research" => "You are SearchInOne's analysis model. Answer using the supplied web-search context. Distinguish source information from reasoning and say when the sources are insufficient.",
            _ => "You are ALLINONE, a helpful general AI assistant. Be accurate, concise, and transparent about uncertainty."
        };

        var input = string.IsNullOrWhiteSpace(context)
            ? prompt
            : $"User request:\n{prompt}\n\nSearchInOne context:\n{context}";

        var payload = new { model = ModelId, instructions, input, store = false };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        request.Headers.UserAgent.ParseAdd("ALLINONE/0.5");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = "Unknown model error.";
            try
            {
                using var error = JsonDocument.Parse(body);
                if (error.RootElement.TryGetProperty("error", out var errorObject) &&
                    errorObject.TryGetProperty("message", out var messageElement))
                    message = messageElement.GetString() ?? message;
            }
            catch (JsonException) { }
            throw new InvalidOperationException($"Model request failed ({(int)response.StatusCode}): {message}");
        }

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("output_text", out var outputText))
        {
            var text = outputText.GetString();
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        }

        var collected = new StringBuilder();
        if (document.RootElement.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var parts) || parts.ValueKind != JsonValueKind.Array) continue;
                foreach (var part in parts.EnumerateArray())
                    if (part.TryGetProperty("text", out var textPart))
                        collected.AppendLine(textPart.GetString());
            }
        }

        var result = collected.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? "The model returned no text." : result;
    }
}
