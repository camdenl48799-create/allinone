namespace ALLINONE.Connectors;

public enum ConnectorConnectionState
{
    Disconnected,
    Connected,
    Error
}

public sealed record ConnectorStatus(
    string Id,
    string DisplayName,
    ConnectorConnectionState State,
    string? Detail = null);

/// <summary>
/// Clean interface so additional external services can be added without rewriting ALLINONE.
/// Architecture: @customconnector → CustomConnectorManager → Selected Connector → External Service
/// </summary>
public interface IConnector
{
    string Id { get; }
    string DisplayName { get; }
    ConnectorStatus Status { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<string> ExecuteAsync(string action, IReadOnlyDictionary<string, string> args, CancellationToken cancellationToken = default);
}
