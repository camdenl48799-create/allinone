using System.IO;
using System.Text.Json;

namespace ALLINONE;

public sealed class ProjectStore
{
    private readonly string root;
    private readonly JsonSerializerOptions json = new() { WriteIndented = true };

    public ProjectStore(string dataDir)
    {
        root = Path.Combine(dataDir, "Projects");
        Directory.CreateDirectory(root);
    }

    public IReadOnlyList<ProjectInfo> List()
    {
        var result = new List<ProjectInfo>();
        foreach (var file in Directory.EnumerateFiles(root, "*.json"))
        {
            try
            {
                var project = JsonSerializer.Deserialize<ProjectInfo>(File.ReadAllText(file));
                if (project is not null) result.Add(project);
            }
            catch { }
        }
        return result.OrderByDescending(x => x.UpdatedUtc).ToList();
    }

    public ProjectInfo Create(string name)
    {
        var project = new ProjectInfo(Guid.NewGuid().ToString("N"), name.Trim(), DateTimeOffset.UtcNow, []);
        Save(project);
        return project;
    }

    public void Save(ProjectInfo project)
    {
        var safe = string.Concat(project.Id.Where(c => char.IsLetterOrDigit(c)));
        File.WriteAllText(Path.Combine(root, safe + ".json"), JsonSerializer.Serialize(project, json));
    }

    public void AddFile(ProjectInfo project, string path)
    {
        if (!File.Exists(path)) return;
        var files = project.Files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!files.Contains(path, StringComparer.OrdinalIgnoreCase)) files.Add(path);
        Save(project with { Files = files, UpdatedUtc = DateTimeOffset.UtcNow });
    }
}

public sealed record ProjectInfo(string Id, string Name, DateTimeOffset UpdatedUtc, IReadOnlyList<string> Files);
