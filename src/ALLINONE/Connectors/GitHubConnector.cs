using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ALLINONE.Connectors;

public sealed class GitHubConnector : IConnector
{
    private const string ApiBase = "https://api.github.com/";
    private readonly SecureCredentialStore store;
    private readonly HttpClient http = new();
    private ConnectorConnectionState state = ConnectorConnectionState.Disconnected;
    private string? detail;

    public GitHubConnector(SecureCredentialStore store)
    {
        this.store = store;
        http.BaseAddress = new Uri(ApiBase);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ALLINONE-CustomConnector/1.0");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public string Id => "github";
    public string DisplayName => "GitHub";
    public ConnectorStatus Status => new(Id, DisplayName, state, detail);

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var token = store.Load(Id);
        state = string.IsNullOrWhiteSpace(token) ? ConnectorConnectionState.Disconnected : ConnectorConnectionState.Connected;
        detail = state == ConnectorConnectionState.Connected ? "Credential stored locally." : "No GitHub token configured.";
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        store.Delete(Id);
        state = ConnectorConnectionState.Disconnected;
        detail = "Credential removed.";
        return Task.CompletedTask;
    }

    public async Task<string> ExecuteAsync(string action, IReadOnlyDictionary<string, string> args, CancellationToken cancellationToken = default)
    {
        if (action.Equals("set-token", StringComparison.OrdinalIgnoreCase))
        {
            if (!args.TryGetValue("token", out var token) || string.IsNullOrWhiteSpace(token))
                return "Provide a token with token=<value>.";
            store.Save(Id, token.Trim());
            await ConnectAsync(cancellationToken);
            return "GitHub connected. The credential is stored using Windows DPAPI and is not displayed.";
        }

        if (action.Equals("disconnect", StringComparison.OrdinalIgnoreCase))
        {
            await DisconnectAsync();
            return "GitHub disconnected and its stored credential was removed.";
        }

        if (!await EnsureConnectedAsync(cancellationToken))
            return "GitHub is not connected. Use @customconnector github set-token token=<your-token>.";

        return action.ToLowerInvariant() switch
        {
            "status" => $"GitHub: {Status.State}. {Status.Detail}",
            "repos" => await GetAsync("user/repos?per_page=30&sort=updated", cancellationToken),
            "branches" => await GetRequiredAsync(args, "repo", "repos/{repo}/branches?per_page=100", cancellationToken),
            "commits" => await GetRequiredAsync(args, "repo", "repos/{repo}/commits?per_page=30", cancellationToken),
            "issues" => await GetRequiredAsync(args, "repo", "repos/{repo}/issues?state=all&per_page=30", cancellationToken),
            "prs" => await GetRequiredAsync(args, "repo", "repos/{repo}/pulls?state=all&per_page=30", cancellationToken),
            "read-file" => await ReadFileAsync(args, cancellationToken),
            "search" => await SearchAsync(args, cancellationToken),
            "create-branch" => await CreateBranchAsync(args, cancellationToken),
            "create-file" => await CreateFileAsync(args, cancellationToken),
            "create-pr" => await CreatePrAsync(args, cancellationToken),
            "comment" => await CommentAsync(args, cancellationToken),
            _ => "Supported GitHub actions: status, repos, branches, commits, issues, prs, read-file, search, set-token, disconnect, create-branch, create-file, create-pr, comment."
        };
    }

    private async Task<bool> EnsureConnectedAsync(CancellationToken ct)
    {
        await ConnectAsync(ct);
        return state == ConnectorConnectionState.Connected;
    }

    private async Task<string> GetAsync(string path, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) return $"GitHub request failed ({(int)response.StatusCode}): {body}";
        return FormatJson(body);
    }

    private async Task<string> GetRequiredAsync(IReadOnlyDictionary<string,string> args, string key, string template, CancellationToken ct)
    {
        if (!args.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            return $"Missing {key}=owner/repository.";
        return await GetAsync(template.Replace("{repo}", Uri.EscapeDataString(value.Trim())), ct);
    }

    private async Task<string> ReadFileAsync(IReadOnlyDictionary<string,string> args, CancellationToken ct)
    {
        if (!args.TryGetValue("repo", out var repo) || !args.TryGetValue("path", out var path))
            return "Use repo=owner/repository path=path/to/file.";
        var branch = args.TryGetValue("branch", out var b) ? $"?ref={Uri.EscapeDataString(b)}" : "";
        return await GetAsync($"repos/{repo.Trim()}/contents/{path.Trim()}{branch}", ct);
    }

    private async Task<string> SearchAsync(IReadOnlyDictionary<string,string> args, CancellationToken ct)
    {
        if (!args.TryGetValue("q", out var q) || string.IsNullOrWhiteSpace(q))
            return "Use q=search terms. Optional repo=owner/repository.";
        var suffix = args.TryGetValue("repo", out var repo) && !string.IsNullOrWhiteSpace(repo) ? $"+repo:{repo.Trim()}" : "";
        return await GetAsync($"search/code?q={Uri.EscapeDataString(q.Trim())}{suffix}", ct);
    }

    private async Task<string> CreateBranchAsync(IReadOnlyDictionary<string,string> args, CancellationToken ct)
    {
        if (!Confirmed(args)) return "Write blocked. Add confirm=true to create a branch.";
        if (!args.TryGetValue("repo", out var repo) || !args.TryGetValue("branch", out var name) || !args.TryGetValue("from", out var from))
            return "Use repo=owner/repository branch=new-name from=source-branch.";
        var source = await GetAsync($"repos/{repo}/git/ref/heads/{Uri.EscapeDataString(from)}", ct);
        using var json = JsonDocument.Parse(source);
        if (!json.RootElement.TryGetProperty("object", out var obj) || !obj.TryGetProperty("sha", out var sha))
            return source;
        return await SendJsonAsync(HttpMethod.Post, $"repos/{repo}/git/refs", new { @ref = "refs/heads/" + name, sha = sha.GetString() }, ct);
    }

    private async Task<string> CreateFileAsync(IReadOnlyDictionary<string,string> args, CancellationToken ct)
    {
        if (!Confirmed(args)) return "Write blocked. Add confirm=true to create a file.";
        if (!args.TryGetValue("repo", out var repo) || !args.TryGetValue("path", out var path) || !args.TryGetValue("content", out var content))
            return "Use repo=owner/repository path=file content="...". Optional branch=branch.";
        var payload = new Dictionary<string, object?> { ["message"] = args.TryGetValue("message", out var msg) ? msg : "Create file via ALLINONE", ["content"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content)) };
        if (args.TryGetValue("branch", out var branch)) payload["branch"] = branch;
        return await SendJsonAsync(HttpMethod.Put, $"repos/{repo}/contents/{path}", payload, ct);
    }

    private async Task<string> CreatePrAsync(IReadOnlyDictionary<string,string> args, CancellationToken ct)
    {
        if (!Confirmed(args)) return "Write blocked. Add confirm=true to create a pull request.";
        if (!args.TryGetValue("repo", out var repo) || !args.TryGetValue("title", out var title) || !args.TryGetValue("head", out var head) || !args.TryGetValue("base", out var baseBranch))
            return "Use repo=owner/repository title="..." head=branch base=main.";
        var body = args.TryGetValue("body", out var b) ? b : "";
        return await SendJsonAsync(HttpMethod.Post, $"repos/{repo}/pulls", new { title, head, @base = baseBranch, body }, ct);
    }

    private async Task<string> CommentAsync(IReadOnlyDictionary<string,string> args, CancellationToken ct)
    {
        if (!Confirmed(args)) return "Write blocked. Add confirm=true to post a comment.";
        if (!args.TryGetValue("repo", out var repo) || !args.TryGetValue("issue", out var issue) || !args.TryGetValue("body", out var body))
            return "Use repo=owner/repository issue=123 body="comment".";
        return await SendJsonAsync(HttpMethod.Post, $"repos/{repo}/issues/{issue}/comments", new { body }, ct);
    }

    private async Task<string> SendJsonAsync(HttpMethod method, string path, object payload, CancellationToken ct)
    {
        using var request = CreateRequest(method, path);
        request.Content = JsonContent.Create(payload);
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) return $"GitHub request failed ({(int)response.StatusCode}): {body}";
        return FormatJson(body);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        var token = store.Load(Id);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static bool Confirmed(IReadOnlyDictionary<string,string> args) =>
        args.TryGetValue("confirm", out var value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static string FormatJson(string json)
    {
        try { return JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement, new JsonSerializerOptions { WriteIndented = true }); }
        catch { return json; }
    }
}
