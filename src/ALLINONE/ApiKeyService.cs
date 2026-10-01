using System.Security.Cryptography;
using System.Text.Json;

namespace ALLINONE;

public sealed class ApiKeyService
{
    private readonly string path;
    private List<ApiKeyRecord> keys = [];

    public ApiKeyService(string dataDir)
    {
        path = Path.Combine(dataDir, "api-keys.json");
        Load();
    }

    public IReadOnlyList<ApiKeyRecord> Keys => keys;

    public (ApiKeyRecord Record, string Secret) Create(string name)
    {
        var secret = "allinone_sk_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var record = new ApiKeyRecord(
            Guid.NewGuid().ToString("N"),
            string.IsNullOrWhiteSpace(name) ? "My ALLINONE key" : name.Trim(),
            Hash(secret),
            DateTimeOffset.UtcNow,
            null,
            true);
        keys.Add(record);
        Save();
        return (record, secret);
    }

    public bool Revoke(string id)
    {
        var index = keys.FindIndex(k => k.Id == id);
        if (index < 0) return false;
        keys[index] = keys[index] with { Revoked = true };
        Save();
        return true;
    }

    public bool Validate(string secret)
    {
        var hash = Hash(secret);
        return keys.Any(k => k.Active && CryptographicOperations.FixedTimeEquals(Convert.FromHexString(k.SecretHash), Convert.FromHexString(hash)));
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(path)) keys = JsonSerializer.Deserialize<List<ApiKeyRecord>>(File.ReadAllText(path)) ?? [];
        }
        catch { keys = []; }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(keys, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed record ApiKeyRecord(
    string Id,
    string Name,
    string SecretHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    bool Revoked)
{
    public bool Active => !Revoked;
}
