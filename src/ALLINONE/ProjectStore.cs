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
                if (project is not null && !string.IsNullOrWhiteSpace(project.Name))
                    result.Add(project);
            }
            catch
            {
                // Ignore one damaged project instead of breaking the whole workspace.
            }
        }

        return result
            .OrderByDescending(x => x.UpdatedUtc)
            .ToList();
    }

    public ProjectInfo Create(string name)
    {
        var cleanName = string.Join(
            ' ',
            name.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(cleanName))
            throw new ArgumentException("Project name cannot be empty.", nameof(name));

        if (cleanName.Length > 80)
            cleanName = cleanName[..80];

        var project = new ProjectInfo(
            Guid.NewGuid().ToString("N"),
            cleanName,
            DateTimeOffset.UtcNow,
            []);

        Save(project);
        return project;
    }

    public void Save(ProjectInfo project)
    {
        var safe = string.Concat(project.Id.Where(char.IsLetterOrDigit));
        if (string.IsNullOrWhiteSpace(safe))
            throw new InvalidOperationException("Invalid project identifier.");

        var normalized = project with
        {
            Name = project.Name.Trim(),
            Files = project.Files
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            UpdatedUtc = DateTimeOffset.UtcNow
        };

        File.WriteAllText(
            Path.Combine(root, safe + ".json"),
            JsonSerializer.Serialize(normalized, json));
    }

    public void AddFile(ProjectInfo project, string path)
    {
        if (!File.Exists(path)) return;

        var files = project.Files
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!files.Contains(path, StringComparer.OrdinalIgnoreCase))
            files.Add(path);

        Save(project with { Files = files });
    }

    public ProjectInfo RemoveFile(ProjectInfo project, string path)
    {
        var files = project.Files
            .Where(x => !x.Equals(path, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var updated = project with { Files = files };
        Save(updated);
        return updated;
    }

    public void Delete(ProjectInfo project)
    {
        var safe = string.Concat(project.Id.Where(char.IsLetterOrDigit));
        if (string.IsNullOrWhiteSpace(safe)) return;

        var path = Path.Combine(root, safe + ".json");
        if (File.Exists(path))
            File.Delete(path);
    }
}

public sealed record ProjectInfo(
    string Id,
    string Name,
    DateTimeOffset UpdatedUtc,
    IReadOnlyList<string> Files);
