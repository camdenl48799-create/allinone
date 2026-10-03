using System.Text;

namespace ALLINONE;

public sealed class FileContextService
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",".md",".json",".csv",".xml",".html",".htm",".css",".js",".ts",".tsx",".jsx",".cs",".cpp",".h",".hpp",".py",".java",".go",".rs",".swift",".kt",".xaml",".yml",".yaml",".sql",".log"
    };

    public async Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("The selected file could not be found.", path);
        var extension = Path.GetExtension(path);
        if (!TextExtensions.Contains(extension))
            throw new NotSupportedException("This build can directly analyze text/code files. PDF, Office, and binary extraction are not yet connected.");

        var info = new FileInfo(path);
        if (info.Length > 2_000_000)
            throw new IOException("The selected text file is larger than the 2 MB local-analysis limit.");

        return await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
    }
}
