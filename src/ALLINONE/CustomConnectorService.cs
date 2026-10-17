using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ALLINONE;

public sealed class CustomConnector
{
    public string Name { get; set; } = "Custom Connector";
    public string BaseUrl { get; set; } = "";
    public string Description { get; set; } = "";
    public List<CustomConnectorTool> Tools { get; set; } = new();
}

public sealed class CustomConnectorTool
{
    public string Name { get; set; } = "";
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class CustomConnectorService
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<string> InvokeAsync(CustomConnector connector, CustomConnectorTool tool, string? jsonBody = null, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(new Uri(connector.BaseUrl, UriKind.Absolute), tool.Path, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("Connector URL must use http or https.");

        using var request = new HttpRequestMessage(new HttpMethod(tool.Method.ToUpperInvariant()), uri);
        if (!string.IsNullOrWhiteSpace(jsonBody))
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        try
        {
            using var document = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
