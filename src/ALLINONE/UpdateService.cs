using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace ALLINONE;

public sealed class UpdateService
{
    private const string Owner = "camdenl48799-create";
    private const string Repo = "allinone";
    private static readonly TimeSpan AutomaticCheckInterval = TimeSpan.FromDays(14);
    private static readonly HttpClient Http = CreateClient();

    private readonly string statePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ALLINONE", "update-state.json");

    private static Version CurrentVersion => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ALLINONE-Updater/1.2");
        return client;
    }

    public Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default) =>
        CheckCoreAsync(force: false, cancellationToken);

    public Task<UpdateInfo?> CheckNowAsync(CancellationToken cancellationToken = default) =>
        CheckCoreAsync(force: true, cancellationToken);

    private async Task<UpdateInfo?> CheckCoreAsync(bool force, CancellationToken cancellationToken)
    {
        if (!force && !ShouldCheckAutomatically())
            return null;

        try
        {
            MarkChecked();
            using var response = await Http.GetAsync($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest", cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken);
            if (release is null || release.draft || release.prerelease)
                return null;

            var latest = ParseVersion(release.tag_name);
            if (latest <= CurrentVersion)
                return null;

            var zip = release.assets?.FirstOrDefault(a => a.name.Equals("ALLINONE-win-x64.zip", StringComparison.OrdinalIgnoreCase));
            var checksum = release.assets?.FirstOrDefault(a => a.name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase));
            if (zip is null || checksum is null)
                return null;

            return new UpdateInfo(latest, release.name ?? release.tag_name, zip.browser_download_url, checksum.browser_download_url);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> InstallAsync(UpdateInfo update, CancellationToken cancellationToken = default)
    {
        var temp = Path.Combine(Path.GetTempPath(), "ALLINONE-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var zipPath = Path.Combine(temp, "ALLINONE-win-x64.zip");
        var checksumPath = Path.Combine(temp, "checksums.txt");

        try
        {
            await DownloadAsync(update.AssetUrl, zipPath, cancellationToken);
            await DownloadAsync(update.ChecksumUrl, checksumPath, cancellationToken);

            var expected = FindChecksum(await File.ReadAllTextAsync(checksumPath, cancellationToken), Path.GetFileName(zipPath));
            if (string.IsNullOrWhiteSpace(expected))
                return false;

            await using (var stream = File.OpenRead(zipPath))
            {
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            var extractPath = Path.Combine(temp, "package");
            ZipFile.ExtractToDirectory(zipPath, extractPath);

            var currentExe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
                return false;

            var packageExe = Path.Combine(extractPath, "ALLINONE.exe");
            if (!File.Exists(packageExe))
                return false;

            var targetDir = Path.GetDirectoryName(currentExe);
            if (string.IsNullOrWhiteSpace(targetDir))
                return false;

            var updateScript = Path.Combine(temp, "apply-update.ps1");
            var script = @"
param([int]$PidToWait, [string]$PackageDir, [string]$TargetDir, [string]$ExePath)
try {
  Wait-Process -Id $PidToWait -Timeout 60 -ErrorAction SilentlyContinue
  Start-Sleep -Milliseconds 700
  Copy-Item -Path (Join-Path $PackageDir '*') -Destination $TargetDir -Recurse -Force
  Start-Process -FilePath $ExePath
} catch {
  exit 1
}
";
            await File.WriteAllTextAsync(updateScript, script, cancellationToken);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{updateScript}\" -PidToWait {Environment.ProcessId} -PackageDir \"{extractPath}\" -TargetDir \"{targetDir}\" -ExePath \"{currentExe}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool ShouldCheckAutomatically()
    {
        try
        {
            if (!File.Exists(statePath))
                return true;
            var state = JsonSerializer.Deserialize<UpdateState>(File.ReadAllText(statePath));
            return state?.LastCheckedUtc is null || DateTime.UtcNow - state.LastCheckedUtc.Value >= AutomaticCheckInterval;
        }
        catch { return true; }
    }

    private void MarkChecked()
    {
        try
        {
            var directory = Path.GetDirectoryName(statePath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(statePath, JsonSerializer.Serialize(new UpdateState(DateTime.UtcNow), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static async Task DownloadAsync(string url, string path, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(path);
        await input.CopyToAsync(output, cancellationToken);
    }

    private static string? FindChecksum(string text, string fileName)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[^1].TrimStart('*').Equals(fileName, StringComparison.OrdinalIgnoreCase))
                return parts[0];
        }
        return null;
    }

    private static Version ParseVersion(string tag)
    {
        var clean = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(clean, out var version) ? version : new Version(0, 0, 0);
    }

    private sealed record GitHubRelease(string tag_name, string? name, bool draft, bool prerelease, GitHubAsset[]? assets);
    private sealed record GitHubAsset(string name, string browser_download_url);
    private sealed record UpdateState(DateTime LastCheckedUtc);
}

public sealed record UpdateInfo(Version Version, string Name, string AssetUrl, string ChecksumUrl);