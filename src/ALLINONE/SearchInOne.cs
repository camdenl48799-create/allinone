using System.Diagnostics;

namespace ALLINONE;

public sealed class SearchInOne
{
    public IReadOnlyList<SearchSource> BuildSources(string query)
    {
        var encoded = Uri.EscapeDataString(query.Trim());
        return
        [
            new SearchSource("Google AI Mode", $"https://www.google.com/search?udm=50&q={encoded}", "Research through Google AI Mode"),
            new SearchSource("Google Search", $"https://www.google.com/search?q={encoded}", "Web search"),
            new SearchSource("Google Maps", $"https://www.google.com/maps/search/?api=1&query={encoded}", "Places and maps"),
            new SearchSource("YouTube", $"https://www.youtube.com/results?search_query={encoded}", "Public videos and channels"),
            new SearchSource("TikTok", $"https://www.tiktok.com/search?q={encoded}", "Public TikTok search")
        ];
    }

    public void Open(SearchSource source) =>
        Process.Start(new ProcessStartInfo(source.Url) { UseShellExecute = true });

    public void OpenResearch(string query) => Open(BuildSources(query)[0]);
}

public sealed record SearchSource(string Name, string Url, string Purpose);
