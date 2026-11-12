using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Orchestrates APS (Autodesk Platform Services) 3-legged OAuth flow.
/// Shows a WebView2 sign-in window, exchanges the auth code for tokens,
/// fetches the user profile, and optionally enforces domain restrictions.
/// </summary>
public class AuthService
{
    private const string BaseUrl      = "https://developer.api.autodesk.com";
    private const string AuthEndpoint = "https://developer.api.autodesk.com/authentication/v2/authorize";
    private const string TokenEndpoint= "https://developer.api.autodesk.com/authentication/v2/token";
    private const string ProfileUrl   = "https://api.userprofile.autodesk.com/userinfo";
    private const string RedirectUri  = "http://localhost:8080/";
    private const string Scope        = "user-profile:read data:read viewables:read";

    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(20) };

    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _state = Guid.NewGuid().ToString("N");
    private readonly string? _codeVerifier;
    private static readonly SemaphoreSlim CloudSignIn = new(1, 1);
    private static string? _cachedClientId;

    // Cached tokens — survive the lifetime of the Revit session.
    private static string? _cachedAccessToken;
    private static string? _cachedEmail;
    private static DateTime _tokenExpiry = DateTime.MinValue;

    public AuthService(string clientId, string clientSecret)
    {
        _clientId     = clientId;
        _clientSecret = clientSecret;
        if (string.IsNullOrEmpty(clientSecret))
        {
            var bytes = new byte[32];
            using var random = RandomNumberGenerator.Create();
            random.GetBytes(bytes);
            _codeVerifier = Base64Url(bytes);
        }
    }

    /// <summary>
    /// Returns the currently cached email (non-null after a successful auth).
    /// </summary>
    public static string? CachedEmail => _cachedEmail;

    /// <summary>
    /// Returns the current access token if it has not expired.
    /// </summary>
    public static string? CachedAccessToken =>
        _cachedAccessToken != null && DateTime.UtcNow < _tokenExpiry ? _cachedAccessToken : null;

    /// <summary>
    /// Full authentication flow. Shows the sign-in window, exchanges the code,
    /// fetches the profile, enforces domain restrictions, identifies the user
    /// in PostHog, and returns the user's email.
    ///
    /// Returns null if the user cancels. Throws on auth errors or domain rejection.
    /// </summary>
    public async Task<string?> AuthenticateUserAsync(string[] allowedDomains)
    {
        // Fast-path: already authenticated this session and token still valid.
        if (_cachedClientId == _clientId && _cachedEmail != null && DateTime.UtcNow < _tokenExpiry)
            return _cachedEmail;

        if (string.IsNullOrWhiteSpace(_clientId))
            throw new InvalidOperationException(
                "APS credentials not configured. Ensure APS_CLIENT_ID is set or modena-config.json is present.");

        var authUrl = BuildAuthUrl();
        var code    = ShowSignInWindow(authUrl);
        if (code == null) return null; // user cancelled

        var (accessToken, expiresIn) = await ExchangeCodeAsync(code).ConfigureAwait(false);
        _cachedAccessToken = accessToken;
        _cachedClientId = _clientId;
        _tokenExpiry       = DateTime.UtcNow.AddSeconds(expiresIn - 60);

        var email = await FetchEmailAsync(accessToken).ConfigureAwait(false);

        // Domain restriction is intentionally disabled — all authenticated users are welcome.
        // Uncomment the block below and set AllowedDomains in modena-config.json to restrict by org.
        //
        // if (allowedDomains.Length > 0)
        // {
        //     var domain = email.Split('@').LastOrDefault() ?? string.Empty;
        //     if (!allowedDomains.Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase)))
        //         throw new UnauthorizedAccessException(
        //             $"Your account ({email}) is not authorised to use this tool.\n" +
        //             $"Allowed domains: {string.Join(", ", allowedDomains)}");
        // }

        _cachedEmail = email;
        AnalyticsService.IdentifyUser(email);
        LogService.Info($"AuthService: Authenticated as {email}");
        return email;
    }

    /// <summary>Cloud access is requested only after Pulse renders; automatic refreshes never show sign-in.</summary>
    public async Task<string> GetCloudAccessTokenAsync(bool allowSignIn, CancellationToken ct)
    {
        await CloudSignIn.WaitAsync(ct);
        try
        {
            if (_cachedClientId == _clientId && CachedAccessToken is { } cached) return cached;
            if (!allowSignIn) throw new CloudModelException("Autodesk sign-in is required. Use Refresh to sign in.");
            ct.ThrowIfCancellationRequested();
            var window = new Views.AuthWindow(BuildAuthUrl());
            var code = await window.ShowForCodeAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (code is null) throw new CloudModelException("Autodesk sign-in was cancelled.");
            var (token, expiresIn) = await ExchangeCodeAsync(code, ct);
            ct.ThrowIfCancellationRequested();
            _cachedClientId = _clientId;
            _cachedAccessToken = token;
            _cachedEmail = null;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(0, expiresIn - 60));
            return token;
        }
        finally { CloudSignIn.Release(); }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private string BuildAuthUrl()
    {
        var parts = new List<string>
        {
            "response_type=code",
            $"client_id={Uri.EscapeDataString(_clientId)}",
            $"redirect_uri={Uri.EscapeDataString(RedirectUri)}",
            $"scope={Uri.EscapeDataString(Scope)}",
            $"state={_state}"
        };
        if (_codeVerifier is not null)
        {
            using var sha = SHA256.Create();
            parts.Add("code_challenge=" + Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(_codeVerifier))));
            parts.Add("code_challenge_method=S256");
        }
        return $"{AuthEndpoint}?{string.Join("&", parts)}";
    }

    private static string? ShowSignInWindow(string authUrl)
    {
        string? capturedCode = null;

        // Must run on STA UI thread — Revit's Execute() is already STA.
        var window = new Views.AuthWindow(authUrl);
        window.CodeCaptured += code => capturedCode = code;

        var result = window.ShowDialog();
        return result == true ? capturedCode : null;
    }

    private async Task<(string accessToken, int expiresIn)> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));

        var values = new List<KeyValuePair<string,string>>
        {
            new KeyValuePair<string,string>("grant_type",   "authorization_code"),
            new KeyValuePair<string,string>("code",         code),
            new KeyValuePair<string,string>("redirect_uri", RedirectUri)
        };
        if (_codeVerifier is not null)
        {
            values.Add(new KeyValuePair<string,string>("client_id", _clientId));
            values.Add(new KeyValuePair<string,string>("code_verifier", _codeVerifier));
        }
        var form = new FormUrlEncodedContent(values);

        using var req = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint) { Content = form };
        if (_codeVerifier is null) req.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new CloudModelException($"Autodesk token exchange failed (HTTP {(int)resp.StatusCode}).");

        var json = JObject.Parse(body);
        var token      = json.Value<string>("access_token")
                         ?? throw new InvalidOperationException("No access_token in token response.");
        var expiresIn  = json.Value<int?>("expires_in") ?? 3600;
        return (token, expiresIn);
    }

    private static async Task<string> FetchEmailAsync(string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, ProfileUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var resp = await Http.SendAsync(req).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Profile fetch failed (HTTP {(int)resp.StatusCode}).");

        var json  = JObject.Parse(body);
        var email = json.Value<string>("email") ?? json.Value<string>("emailId");

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Could not determine email from APS profile response.");

        return email;
    }
}
