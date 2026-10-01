namespace ALLINONE.Connectors;

/// <summary>
/// @customconnector entry point.
/// Routes to the selected connector implementation.
/// Separate from @One-Api (API key management).
/// </summary>
public sealed class CustomConnectorManager
{
    private readonly Dictionary<string, IConnector> connectors = new(StringComparer.OrdinalIgnoreCase);
    private readonly SecureCredentialStore store;

    public CustomConnectorManager(string dataDir)
    {
        store = new SecureCredentialStore(dataDir);
        Register(new GitHubConnector(store));
    }

    public void Register(IConnector connector) => connectors[connector.Id] = connector;

    public IReadOnlyList<ConnectorStatus> AllStatus() =>
        connectors.Values.Select(c => c.Status).ToList();

    public async Task<string> HandleAsync(string command, CancellationToken cancellationToken = default)
    {
        var parts = SplitArgs(command);
        if (parts.Count == 0)
        {
            var list = string.Join("\n", AllStatus().Select(s =>
                $"• {s.DisplayName} ({s.Id}): {s.State}" + (s.Detail is null ? "" : $" — {s.Detail}")));
            return "Custom connectors:\n" + list +
                   "\n\nUsage: @customconnector <connector> <action> [key=value …]\n" +
                   "Example: @customconnector github repos\n" +
                   "Example: @customconnector github set-token token=ghp_…";
        }

        var connectorId = parts[0];
        if (!connectors.TryGetValue(connectorId, out var connector))
            return $"Unknown connector '{connectorId}'. Available: {string.Join(", ", connectors.Keys)}";

        var action = parts.Count > 1 ? parts[1] : "status";
        var args = ParseKeyValues(parts.Skip(2).ToList());

        if (action is "set-token" or "connect" && !args.ContainsKey("token") && parts.Count > 2)
            args["token"] = parts[2];

        return await connector.ExecuteAsync(action, args, cancellationToken);
    }

    private static List<string> SplitArgs(string command)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        foreach (var ch in command.Trim())
        {
            if (ch == '"') { inQuotes = !inQuotes; continue; }
            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (current.Length > 0) { result.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(ch);
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    private static Dictionary<string, string> ParseKeyValues(List<string> tokens)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            var eq = token.IndexOf('=');
            if (eq > 0)
                dict[token[..eq]] = token[(eq + 1)..];
        }
        return dict;
    }
}
