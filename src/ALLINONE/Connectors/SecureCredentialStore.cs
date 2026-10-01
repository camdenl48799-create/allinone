using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ALLINONE.Connectors;

/// <summary>
/// Stores connector secrets separately from normal ALLINONE settings, using DPAPI.
/// Secrets are never written to chat, logs, or source.
/// </summary>
public sealed class SecureCredentialStore
{
    private readonly string dir;

    public SecureCredentialStore(string dataDir)
    {
        dir = Path.Combine(dataDir, "connectors");
        Directory.CreateDirectory(dir);
    }

    public void Save(string connectorId, string secret)
    {
        var path = Path.Combine(dir, Sanitize(connectorId) + ".dat");
        var bytes = Encoding.UTF8.GetBytes(secret);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedBytes);
    }

    public string? Load(string connectorId)
    {
        var path = Path.Combine(dir, Sanitize(connectorId) + ".dat");
        if (!File.Exists(path)) return null;
        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    public void Delete(string connectorId)
    {
        var path = Path.Combine(dir, Sanitize(connectorId) + ".dat");
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public bool Has(string connectorId) => File.Exists(Path.Combine(dir, Sanitize(connectorId) + ".dat"));

    private static string Sanitize(string id)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            id = id.Replace(c, '_');
        return id.ToLowerInvariant();
    }
}
