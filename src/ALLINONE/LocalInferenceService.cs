using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ALLINONE;

public enum LocalModelProvider
{
    Ollama,
    OpenAiCompatible
}

public sealed record LocalModelConfig(
    LocalModelProvider Provider,
    string Endpoint,
    string Model);

public sealed record LocalModelStatus(
    bool Connected,
    string Message,
    string[] Models);

public sealed class LocalInferenceService
{
    private readonly HttpClient http = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    public async Task<LocalModelStatus> CheckAsync(
        LocalModelConfig config,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(config.Endpoint), UriKind.Absolute, out var baseUri))
            return new(false, "The local AI endpoint is not a valid HTTP URL.", []);

        try
        {
            if (config.Provider == LocalModelProvider.Ollama)
            {
                var url = new Uri(baseUri, "/api/tags");
                using var response = await http.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return new(false, $"Ollama returned HTTP {(int)response.StatusCode}.", []);

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var models = doc.RootElement.TryGetProperty("models", out var items)
                    ? items.EnumerateArray()
                        .Select(x => x.TryGetProperty("name", out var n) ? n.GetString() : null)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Cast<string>()
                        .ToArray()
                    : [];

                return new(true, $"Connected to Ollama ({models.Length} model(s) visible).", models);
            }

            var openAiUrl = new Uri(baseUri, "/v1/models");
            using var openAiResponse = await http.GetAsync(openAiUrl, cancellationToken);
            if (!openAiResponse.IsSuccessStatusCode)
                return new(false, $"Local server returned HTTP {(int)openAiResponse.StatusCode}.", []);

            using var openAiDoc = JsonDocument.Parse(await openAiResponse.Content.ReadAsStringAsync(cancellationToken));
            var openAiModels = openAiDoc.RootElement.TryGetProperty("data", out var data)
                ? data.EnumerateArray()
                    .Select(x => x.TryGetProperty("id", out var id) ? id.GetString() : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToArray()
                : [];

            return new(true, $"Connected to an OpenAI-compatible local server ({openAiModels.Length} model(s) visible).", openAiModels);
        }
        catch (OperationCanceledException)
        {
            return new(false, "Connection check timed out.", []);
        }
        catch (Exception ex)
        {
            return new(false, $"Local AI is unavailable: {ex.Message}", []);
        }
    }

    public async Task<string> GenerateAsync(
        LocalModelConfig config,
        IReadOnlyList<ChatMessage> history,
        string systemPrompt,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(config.Endpoint), UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException("The local AI endpoint is not a valid HTTP URL.");

        if (string.IsNullOrWhiteSpace(config.Model))
            throw new InvalidOperationException("Choose a local model name first.");

        var payloadMessages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        foreach (var message in history.TakeLast(24))
        {
            var role = message.Role.Equals("user", StringComparison.OrdinalIgnoreCase)
                ? "user"
                : "assistant";

            payloadMessages.Add(new { role, content = message.Text });
        }

        object payload = config.Provider == LocalModelProvider.Ollama
            ? new
            {
                model = config.Model.Trim(),
                messages = payloadMessages,
                stream = false,
                options = new { temperature = 0.7 }
            }
            : new
            {
                model = config.Model.Trim(),
                messages = payloadMessages,
                stream = false,
                temperature = 0.7,
                max_tokens = 2048
            };

        var endpoint = config.Provider == LocalModelProvider.Ollama
            ? new Uri(baseUri, "/api/chat")
            : new Uri(baseUri, "/v1/chat/completions");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = TryGetError(body);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(detail)
                    ? $"Local model returned HTTP {(int)response.StatusCode}."
                    : detail);
        }

        return config.Provider == LocalModelProvider.Ollama
            ? ParseOllama(body)
            : ParseOpenAiCompatible(body);
    }

    private static string ParseOllama(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
            throw new InvalidOperationException("The Ollama response did not contain message.content.");

        var text = content.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text)
            ? "The local model returned an empty response."
            : text;
    }

    private static string ParseOpenAiCompatible(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
            choices.GetArrayLength() == 0)
            throw new InvalidOperationException("The local server returned no choices.");

        var message = choices[0].GetProperty("message");
        var text = message.GetProperty("content").GetString()?.Trim();

        return string.IsNullOrWhiteSpace(text)
            ? "The local model returned an empty response."
            : text;
    }

    private static string? TryGetError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString();

                if (error.TryGetProperty("message", out var message))
                    return message.GetString();
            }
        }
        catch { }

        return null;
    }

    private static string NormalizeBaseUrl(string endpoint)
    {
        var clean = (endpoint ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(clean))
            return "http://127.0.0.1:11434";

        return clean.EndsWith('/') ? clean : clean + "/";
    }
}
