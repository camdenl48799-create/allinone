using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ALLINONE;

public sealed class ApiKeyService
{
    private readonly string path;
    private readonly object sync = new();
    private List<ApiKeyRecord> keys = [];

    public ApiKeyService(string dataDir)
    {
        path = Path.Combine(dataDir, "api-keys.json");
        Load();
    }

    public IReadOnlyList<ApiKeyRecord> Keys
    {
        get { lock (sync) return keys.ToArray(); }
    }

    public (ApiKeyRecord Record, string Secret) Create(string name)
    {
        var secret = "allinone_sk_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var record = new ApiKeyRecord(
            Guid.NewGuid().ToString("N"),
            SanitizeName(name),
            Hash(secret),
            DateTimeOffset.UtcNow,
            null,
            false);

        lock (sync)
        {
            keys.Add(record);
            Save();
        }

        return (record, secret);
    }

    public bool Revoke(string id)
    {
        lock (sync)
        {
            var index = keys.FindIndex(k => k.Id == id);
            if (index < 0 || keys[index].Revoked) return false;
            keys[index] = keys[index] with { Revoked = true };
            Save();
            return true;
        }
    }

    public bool Validate(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length > 256) return false;
        var hash = Hash(secret);

        lock (sync)
        {
            foreach (var key in keys)
            {
                if (!key.Active) continue;
                if (CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(key.SecretHash),
                    Convert.FromHexString(hash)))
                {
                    var index = keys.FindIndex(k => k.Id == key.Id);
                    keys[index] = key with { LastUsedAt = DateTimeOffset.UtcNow };
                    Save();
                    return true;
                }
            }
        }

        return false;
    }

    private static string SanitizeName(string? name)
    {
        var value = string.IsNullOrWhiteSpace(name) ? "My ALLINONE key" : name.Trim();
        return value.Length > 80 ? value[..80] : value;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private void Load()
    {
        lock (sync)
        {
            try
            {
                if (!File.Exists(path)) return;
                keys = JsonSerializer.Deserialize<List<ApiKeyRecord>>(File.ReadAllText(path)) ?? [];
            }
            catch
            {
                keys = [];
            }
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(keys, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
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
