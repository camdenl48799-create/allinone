using System.Net.Http;
using System.Net.Http.Headers;

namespace ALLINONE.Connectors;

public sealed class GitHubConnector : IConnector
{
    private readonly HttpClient http = new();
    private ConnectorConnectionState state = ConnectorConnectionState.Disconnected;
    private string? detail;

    public GitHubConnector(SecureCredentialStore store)
    {
        http.BaseAddress = new Uri("https://api.github.com/");
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ALLINONE-CustomConnector/1.0");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        state = ConnectorConnectionState.Connected;
        detail = "Public GitHub access ready.";
    }

    public string Id => "github";
    public string DisplayName => "GitHub";
    public ConnectorStatus Status => new(Id, DisplayName, state, detail);

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        state = ConnectorConnectionState.Connected;
        detail = "Public GitHub access ready.";
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        state = ConnectorConnectionState.Disconnected;
        detail = "Disconnected.";
        return Task.CompletedTask;
    }

    public async Task<string> ExecuteAsync(string action, IReadOnlyDictionary<string, string> args, CancellationToken cancellationToken = default)
    {
        if (action.Equals("status", StringComparison.OrdinalIgnoreCase))
            return $"GitHub: {Status.State}. {Status.Detail}";

        if (action.Equals("disconnect", StringComparison.OrdinalIgnoreCase))
        {
            await DisconnectAsync();
            return "GitHub connector disconnected.";
        }

        if (action.Equals("connect", StringComparison.OrdinalIgnoreCase))
        {
            await ConnectAsync(cancellationToken);
            return "GitHub connector connected.";
        }

        if (state != ConnectorConnectionState.Connected)
            return "GitHub connector is disconnected. Use @customconnector github connect.";

        if (!args.TryGetValue("repo", out var repo) || string.IsNullOrWhiteSpace(repo))
            return "Use repo=owner/repository for GitHub repository actions.";

        var cleanRepo = repo.Trim().Trim('/');
        return action.ToLowerInvariant() switch
        {
            "repos" => await GetAsync($"repos/{cleanRepo}", cancellationToken),
            "branches" => await GetAsync($"repos/{cleanRepo}/branches?per_page=100", cancellationToken),
            "commits" => await GetAsync($"repos/{cleanRepo}/commits?per_page=30", cancellationToken),
            "issues" => await GetAsync($"repos/{cleanRepo}/issues?state=all&per_page=30", cancellationToken),
            "prs" => await GetAsync($"repos/{cleanRepo}/pulls?state=all&per_page=30", cancellationToken),
            "read-file" => await ReadFileAsync(cleanRepo, args, cancellationToken),
            _ => "Supported GitHub actions: status, connect, disconnect, repos, branches, commits, issues, prs, read-file."
        };
    }

    private async Task<string> ReadFileAsync(string repo, IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        if (!args.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
            return "Use repo=owner/repository path=path/to/file.";
        var query = args.TryGetValue("branch", out var branch) && !string.IsNullOrWhiteSpace(branch)
            ? $"?ref={Uri.EscapeDataString(branch)}"
            : "";
        return await GetAsync($"repos/{repo}/contents/{path.Trim()}{query}", ct);
    }

    private async Task<string> GetAsync(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return $"GitHub request failed ({(int)response.StatusCode}).";
        return body;
    }
}
