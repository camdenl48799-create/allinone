using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace ALLINONE;

public sealed class ModelService : IDisposable
{
    private const string OllamaUrl = "http://127.0.0.1:11434";
    private const string ModelName = "qwen3:0.6b";

    private readonly HttpClient http = new() { BaseAddress = new Uri(OllamaUrl) };
    private readonly SemaphoreSlim generationLock = new(1, 1);

    public string ModelId => "Qwen3 0.6B";
    public bool IsConfigured { get; private set; }

    public ModelService()
    {
        IsConfigured = CheckRuntime();
    }

    public async Task<string> GenerateAsync(
        string prompt,
        string? context = null,
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        await generationLock.WaitAsync(cancellationToken);
        try
        {
            if (!await EnsureModelAsync(cancellationToken))
                return "Qwen3 is not installed or Ollama is not running. Install/start the local Ollama runtime and make the qwen3:0.6b model available.";

            var system = role switch
            {
                "code" => "You are CodeInOne, the coding intelligence inside ALLINONE. Write correct, practical code and be honest about what was actually changed.",
                "game" => "You are ALLINONE's game-building intelligence. Help design games, gameplay systems, code, project structure, and safe creative features.",
                "research" => "You are SearchInOne's research intelligence. Use supplied web sources as evidence, distinguish facts from reasoning, and never invent sources.",
                _ => "You are ALLINONE, a helpful AI assistant powered locally by Qwen3. Be accurate, concise, transparent, and useful."
            };

            var fullPrompt = string.IsNullOrWhiteSpace(context)
                ? prompt
                : $"{prompt}\n\nSearchInOne source context:\n{context}";

            var request = new
            {
                model = ModelName,
                stream = false,
                think = false,
                options = new { temperature = 0.6 },
                messages = new[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = fullPrompt }
                }
            };

            using var response = await http.PostAsJsonAsync("/api/chat", request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Local Qwen3 runtime returned {(int)response.StatusCode}: {body}");

            using var json = JsonDocument.Parse(body);
            var answer = json.RootElement.GetProperty("message").GetProperty("content").GetString()?.Trim();

            IsConfigured = true;
            return string.IsNullOrWhiteSpace(answer) ? "Qwen3 returned no text." : answer;
        }
        finally
        {
            generationLock.Release();
        }
    }

    private bool CheckRuntime()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tags");
            using var response = http.Send(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> EnsureModelAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var tags = await http.GetFromJsonAsync<JsonElement>("/api/tags", cancellationToken);
            if (tags.TryGetProperty("models", out var models))
            {
                foreach (var model in models.EnumerateArray())
                {
                    if (model.TryGetProperty("name", out var name) &&
                        string.Equals(name.GetString(), ModelName, StringComparison.OrdinalIgnoreCase))
                    {
                        IsConfigured = true;
                        return true;
                    }
                }
            }

            // Ask the local runtime to pull Qwen3. No third-party API is used.
            using var pull = await http.PostAsJsonAsync("/api/pull", new { name = ModelName, stream = false }, cancellationToken);
            IsConfigured = pull.IsSuccessStatusCode;
            return IsConfigured;
        }
        catch
        {
            IsConfigured = false;
            return false;
        }
    }

    public void Dispose()
    {
        generationLock.Dispose();
        http.Dispose();
    }
}
