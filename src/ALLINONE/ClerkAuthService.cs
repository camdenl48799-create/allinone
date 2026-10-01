using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ALLINONE;

public sealed class ClerkAuthService
{
    public const string DefaultRedirectUri = "http://127.0.0.1:54321/oauth/callback";
    private readonly HttpClient http = new();
    private readonly string tokenPath;

    public ClerkAuthService(string dataDir)
    {
        tokenPath = Path.Combine(dataDir, "clerk-session.dat");
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALLINONE_CLERK_FRONTEND_API_URL")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALLINONE_CLERK_CLIENT_ID"));

    public async Task<ClerkUser?> SignInAsync(string? provider = null, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Clerk is not configured. Set ALLINONE_CLERK_FRONTEND_API_URL and ALLINONE_CLERK_CLIENT_ID.");

        var frontendApi = Environment.GetEnvironmentVariable("ALLINONE_CLERK_FRONTEND_API_URL")!.TrimEnd('/');
        var clientId = Environment.GetEnvironmentVariable("ALLINONE_CLERK_CLIENT_ID")!;
        var authorizeUrl = Environment.GetEnvironmentVariable("ALLINONE_CLERK_AUTHORIZE_URL") ?? $"{frontendApi}/oauth/authorize";
        var tokenUrl = Environment.GetEnvironmentVariable("ALLINONE_CLERK_TOKEN_URL") ?? $"{frontendApi}/oauth/token";
        var redirectUri = Environment.GetEnvironmentVariable("ALLINONE_CLERK_REDIRECT_URI") ?? DefaultRedirectUri;

        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(verifier)));

        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "openid profile email offline_access",
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };

        // Clerk's OAuth authorization screen handles the enabled social connections.
        // The optional provider value is reserved for a provider-specific flow when supported by the configured instance.
                using var listener = new LocalCallbackListener(new Uri(redirectUri));
        var url = authorizeUrl + "?" + string.Join("&", query.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        var callback = await listener.WaitAsync(TimeSpan.FromMinutes(10), cancellationToken);

        if (!string.Equals(callback.State, state, StringComparison.Ordinal))
            throw new InvalidOperationException("Authentication state validation failed.");

        if (!string.IsNullOrWhiteSpace(callback.Error))
            throw new InvalidOperationException($"Clerk authentication failed: {callback.Error}");

        if (string.IsNullOrWhiteSpace(callback.Code))
            throw new InvalidOperationException("Clerk did not return an authorization code.");

        using var tokenResponse = await http.PostAsync(tokenUrl,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = callback.Code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = verifier
            }), cancellationToken);

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"Clerk token exchange failed: {tokenJson}");

        var tokens = JsonSerializer.Deserialize<ClerkTokenResponse>(tokenJson)
                     ?? throw new InvalidOperationException("Clerk returned an invalid token response.");

        await SaveTokensAsync(tokens);

        using var userRequest = new HttpRequestMessage(HttpMethod.Get, $"{frontendApi}/oauth/userinfo");
        userRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var userResponse = await http.SendAsync(userRequest, cancellationToken);
        var userJson = await userResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!userResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"Clerk user lookup failed: {userJson}");

        return JsonSerializer.Deserialize<ClerkUser>(userJson);
    }

    public void SignOut()
    {
        try { if (File.Exists(tokenPath)) File.Delete(tokenPath); } catch { }
    }

    public bool HasStoredSession => File.Exists(tokenPath);

    private async Task SaveTokensAsync(ClerkTokenResponse tokens)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(tokens);
        var protectedBytes = ProtectedData.Protect(json, null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(tokenPath, protectedBytes);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class ClerkTokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("id_token")] public string? IdToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    public sealed class ClerkUser
    {
        public string? user_id { get; set; }
        public string? sub { get; set; }
        public string? name { get; set; }
        public string? given_name { get; set; }
        public string? family_name { get; set; }
        public string? email { get; set; }
        public string? picture { get; set; }
    }

    private sealed class CallbackResult
    {
        public string? Code { get; init; }
        public string? State { get; init; }
        public string? Error { get; init; }
    }

    private sealed class LocalCallbackListener : IDisposable
    {
        private readonly TcpListener listener;
        private readonly Uri redirectUri;

        public LocalCallbackListener(Uri redirectUri)
        {
            this.redirectUri = redirectUri;
            listener = new TcpListener(System.Net.IPAddress.Loopback, redirectUri.Port);
            listener.Start();
        }

        public async Task<CallbackResult> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var client = await listener.AcceptTcpClientAsync(linked.Token);
            await using var stream = client.GetStream();

            var buffer = new byte[8192];
            var count = await stream.ReadAsync(buffer, linked.Token);
            var request = Encoding.UTF8.GetString(buffer, 0, count);
            var firstLine = request.Split("\r\n", StringSplitOptions.None).FirstOrDefault() ?? "";
            var target = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1) ?? "/";
            var callbackUri = new Uri(new Uri($"{redirectUri.Scheme}://{redirectUri.Authority}"), target);
            var query = ParseQuery(callbackUri.Query);

            const string html = "<html><body style='font-family:Segoe UI;text-align:center;padding:48px'><h2>ALLINONE authentication complete</h2><p>You can close this tab and return to ALLINONE.</p></body></html>";
            var response = $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(html)}\r\nConnection: close\r\n\r\n{html}";
            var responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, linked.Token);
            client.Close();

            return new CallbackResult
            {
                Code = query.TryGetValue("code", out var code) ? code : null,
                State = query.TryGetValue("state", out var state) ? state : null,
                Error = query.TryGetValue("error", out var error) ? error : null
            };
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var raw = query.TrimStart('?');
            foreach (var part in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pieces = part.Split('=', 2);
                var key = Uri.UnescapeDataString(pieces[0].Replace("+", " "));
                var value = pieces.Length == 2 ? Uri.UnescapeDataString(pieces[1].Replace("+", " ")) : "";
                result[key] = value;
            }
            return result;
        }

        public void Dispose()
        {
            try { listener.Stop(); } catch { }
        }
    }
}
