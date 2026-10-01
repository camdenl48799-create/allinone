using System.Net;
using System.Text;
using System.Text.Json;

namespace ALLINONE;

public sealed class AllInOneApiServer : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly ApiKeyService keys;
    private readonly ModelService model;
    private CancellationTokenSource? cts;

    public const int Port = 47821;
    public string BaseUrl => $"http://127.0.0.1:{Port}/v1/";

    public AllInOneApiServer(ApiKeyService keys, ModelService model)
    {
        this.keys = keys;
        this.model = model;
        listener.Prefixes.Add($"http://127.0.0.1:{Port}/v1/");
    }

    public void Start()
    {
        if (cts is not null) return;
        cts = new CancellationTokenSource();
        listener.Start();
        _ = Task.Run(() => ListenAsync(cts.Token));
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync();
                _ = Task.Run(() => HandleAsync(context, cancellationToken), cancellationToken);
            }
            catch when (cancellationToken.IsCancellationRequested) { break; }
            catch { await Task.Delay(250, cancellationToken); }
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (context.Request.HttpMethod == "OPTIONS") { Write(context, 204, ""); return; }
            var key = ReadApiKey(context.Request);
            if (string.IsNullOrWhiteSpace(key) || !keys.Validate(key))
            {
                WriteJson(context, 401, new { error = new { message = "Invalid or missing ALLINONE API key.", type = "authentication_error" } });
                return;
            }

            var path = context.Request.Url?.AbsolutePath ?? "";
            if (context.Request.HttpMethod == "GET" && path.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context, 200, new { object = "list", data = new[] { new { id = "ALLINONE", @object = "model", owned_by = "ALLINONE" } } });
                return;
            }

            if (context.Request.HttpMethod == "POST" && path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var body = await reader.ReadToEndAsync(cancellationToken);
                using var json = JsonDocument.Parse(body);
                var messages = json.RootElement.TryGetProperty("messages", out var m) ? m : default;
                var prompt = messages.ValueKind == JsonValueKind.Array
                    ? string.Join("\n", messages.EnumerateArray().Where(x => x.TryGetProperty("content", out _)).Select(x => x.GetProperty("content").GetString()))
                    : "";
                var answer = await model.GenerateAsync(prompt, role: "general", cancellationToken: cancellationToken);
                WriteJson(context, 200, new
                {
                    id = "allinone-" + Guid.NewGuid().ToString("N"),
                    @object = "chat.completion",
                    model = "ALLINONE",
                    choices = new[] { new { index = 0, message = new { role = "assistant", content = answer }, finish_reason = "stop" } }
                });
                return;
            }

            WriteJson(context, 404, new { error = new { message = "ALLINONE API route not found." } });
        }
        catch (Exception ex)
        {
            WriteJson(context, 500, new { error = new { message = ex.Message, type = "server_error" } });
        }
    }

    private static string? ReadApiKey(HttpListenerRequest request)
    {
        var auth = request.Headers["Authorization"];
        if (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return auth[7..].Trim();
        return request.Headers["X-ALLINONE-API-Key"];
    }

    private static void WriteJson(HttpListenerContext context, int status, object value)
    {
        Write(context, status, JsonSerializer.Serialize(value), "application/json");
    }

    private static void Write(HttpListenerContext context, int status, string body, string contentType = "text/plain")
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Response.StatusCode = status;
        context.Response.ContentType = contentType;
        context.Response.ContentEncoding = Encoding.UTF8;
        context.Response.ContentLength64 = bytes.Length;
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Authorization, Content-Type, X-ALLINONE-API-Key";
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.Close();
    }

    public void Dispose()
    {
        try { cts?.Cancel(); } catch { }
        listener.Stop();
        listener.Close();
        cts?.Dispose();
    }
}
