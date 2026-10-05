using System.Text.Json;
using System.Text.Json.Serialization;

namespace ALLINONE;

public sealed class ChatStore
{
    private readonly string root;
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ChatStore(string dataDir)
    {
        root = Path.Combine(dataDir, "Chats");
        Directory.CreateDirectory(root);
    }

    public IReadOnlyList<ChatSession> List()
    {
        var result = new List<ChatSession>();

        foreach (var file in Directory.EnumerateFiles(root, "*.json"))
        {
            try
            {
                var session = JsonSerializer.Deserialize<ChatSession>(File.ReadAllText(file), json);
                if (session is not null && !string.IsNullOrWhiteSpace(session.Id))
                    result.Add(session);
            }
            catch
            {
                // Ignore a single damaged chat file.
            }
        }

        return result
            .OrderByDescending(x => x.UpdatedUtc)
            .ToList();
    }

    public ChatSession Create(string title = "New chat")
    {
        var now = DateTimeOffset.UtcNow;
        var session = new ChatSession
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = NormalizeTitle(title),
            CreatedUtc = now,
            UpdatedUtc = now
        };

        Save(session);
        return session;
    }

    public ChatSession? Load(string id)
    {
        var path = GetPath(id);
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<ChatSession>(File.ReadAllText(path), json);
        }
        catch
        {
            return null;
        }
    }

    public void Save(ChatSession session)
    {
        if (string.IsNullOrWhiteSpace(session.Id))
            throw new ArgumentException("Chat ID is required.", nameof(session));

        session.Title = NormalizeTitle(session.Title);
        session.UpdatedUtc = DateTimeOffset.UtcNow;

        File.WriteAllText(
            GetPath(session.Id),
            JsonSerializer.Serialize(session, json));
    }

    public void Delete(ChatSession session)
    {
        var path = GetPath(session.Id);
        if (File.Exists(path))
            File.Delete(path);
    }

    public void Rename(ChatSession session, string title)
    {
        session.Title = NormalizeTitle(title);
        Save(session);
    }

    private string GetPath(string id)
    {
        var safe = string.Concat(id.Where(char.IsLetterOrDigit));
        if (string.IsNullOrWhiteSpace(safe))
            throw new InvalidOperationException("Invalid chat identifier.");

        return Path.Combine(root, safe + ".json");
    }

    private static string NormalizeTitle(string title)
    {
        var clean = string.Join(
            ' ',
            (title ?? string.Empty).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(clean))
            clean = "New chat";

        return clean.Length > 70 ? clean[..70] : clean;
    }
}

public sealed class ChatSession
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "New chat";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<ChatMessage> Messages { get; set; } = [];
}

public sealed class ChatMessage
{
    public string Role { get; set; } = "assistant";
    public string Author { get; set; } = "ALLINONE";
    public string Text { get; set; } = "";
    public DateTimeOffset UtcTime { get; set; } = DateTimeOffset.UtcNow;
}
