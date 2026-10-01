using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace ALLINONE;

public sealed class SearchInOne
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public IReadOnlyList<SearchSource> BuildSources(string query)
    {
        var encoded = Uri.EscapeDataString(query.Trim());
        return
        [
            new SearchSource("Internet", $"https://html.duckduckgo.com/html/?q={encoded}", "Internet-wide web search"),
            new SearchSource("Google Search", $"https://www.google.com/search?q={encoded}", "Web search"),
            new SearchSource("Google Maps", $"https://www.google.com/maps/search/?api=1&query={encoded}", "Places and maps"),
            new SearchSource("YouTube", $"https://www.youtube.com/results?search_query={encoded}", "Public videos and channels"),
            new SearchSource("TikTok", $"https://www.tiktok.com/search?q={encoded}", "Public TikTok search")
        ];
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var encoded = Uri.EscapeDataString(query.Trim());
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://html.duckduckgo.com/html/?q={encoded}");
        request.Headers.UserAgent.ParseAdd("ALLINONE SearchInOne/1.0");
        using var response = await Http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseResults(html);
    }

    private static IReadOnlyList<SearchResult> ParseResults(string html)
    {
        var results = new List<SearchResult>();
        var matches = Regex.Matches(
            html,
            @"<a[^>]*class=""result__a""[^>]*href=""(?<url>[^""]+)""[^>]*>(?<title>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var title = CleanHtml(match.Groups["title"].Value);
            var rawUrl = WebUtility.HtmlDecode(match.Groups["url"].Value);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(rawUrl))
                continue;

            var url = rawUrl.StartsWith("//", StringComparison.Ordinal)
                ? "https:" + rawUrl
                : rawUrl;

            if (Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            {
                var query = parsed.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
                var uddgPart = query.FirstOrDefault(part => part.StartsWith("uddg=", StringComparison.OrdinalIgnoreCase));
                if (uddgPart is not null)
                    url = Uri.UnescapeDataString(uddgPart["uddg=".Length..]);
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                continue;

            results.Add(new SearchResult(title, url));
            if (results.Count >= 8)
                break;
        }

        return results;
    }

    private static string CleanHtml(string value)
    {
        var withoutTags = Regex.Replace(value, "<.*?>", string.Empty);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }

    public void Open(SearchSource source) =>
        Process.Start(new ProcessStartInfo(source.Url) { UseShellExecute = true });

    public void OpenResult(SearchResult result) =>
        Process.Start(new ProcessStartInfo(result.Url) { UseShellExecute = true });
}

public sealed record SearchSource(string Name, string Url, string Purpose);
public sealed record SearchResult(string Title, string Url);
