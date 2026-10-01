using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace ALLINONE;

public sealed class SearchInOne
{
    private static readonly HttpClient Http = CreateClient();

    public IReadOnlyList<SearchSource> BuildSources(string query)
    {
        var clean = query.Trim();
        var encoded = Uri.EscapeDataString(clean);
        return
        [
            new("Internet", $"https://html.duckduckgo.com/html/?q={encoded}", "Internet-wide web search"),
            new("Google Search", $"https://www.google.com/search?q={encoded}", "Web search"),
            new("Google Maps", $"https://www.google.com/maps/search/?api=1&query={encoded}", "Places and maps"),
            new("YouTube", $"https://www.youtube.com/results?search_query={encoded}", "Public videos and channels"),
            new("TikTok", $"https://www.tiktok.com/search?q={encoded}", "Public TikTok search")
        ];
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var encoded = Uri.EscapeDataString(query.Trim());
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://html.duckduckgo.com/html/?q={encoded}");

        using var response = await Http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseResults(html);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ALLINONE SearchInOne/2.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        return client;
    }

    private static IReadOnlyList<SearchResult> ParseResults(string html)
    {
        var results = new List<SearchResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var matches = Regex.Matches(
            html,
            @"<a[^>]*class=""result__a""[^>]*href=""(?<url>[^""]+)""[^>]*>(?<title>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var title = CleanHtml(match.Groups["title"].Value);
            var rawUrl = WebUtility.HtmlDecode(match.Groups["url"].Value);
            var url = UnwrapUrl(rawUrl);

            if (string.IsNullOrWhiteSpace(title) ||
                !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) ||
                !seen.Add(parsed.AbsoluteUri))
                continue;

            results.Add(new SearchResult(title, parsed.AbsoluteUri));
            if (results.Count == 10) break;
        }

        return results;
    }

    private static string UnwrapUrl(string rawUrl)
    {
        if (rawUrl.StartsWith("//", StringComparison.Ordinal))
            rawUrl = "https:" + rawUrl;

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsed))
            return rawUrl;

        var query = parsed.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        var uddg = query.FirstOrDefault(x => x.StartsWith("uddg=", StringComparison.OrdinalIgnoreCase));
        return uddg is null ? rawUrl : Uri.UnescapeDataString(uddg["uddg=".Length..]);
    }

    private static string CleanHtml(string value)
    {
        var withoutTags = Regex.Replace(value, "<.*?>", string.Empty);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }

    public static void Open(SearchSource source) =>
        Process.Start(new ProcessStartInfo(source.Url) { UseShellExecute = true });

    public static void OpenResult(SearchResult result) =>
        Process.Start(new ProcessStartInfo(result.Url) { UseShellExecute = true });
}

public sealed record SearchSource(string Name, string Url, string Purpose);
public sealed record SearchResult(string Title, string Url);
