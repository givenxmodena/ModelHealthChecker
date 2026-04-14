using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Handles 2-legged OAuth (client_credentials) authentication with Autodesk Platform Services.
/// Caches the token in memory and refreshes when within 5 minutes of expiry.
/// </summary>
public class ApsAuthService
{
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly object _lock = new();

    public ApsAuthService(PluginConfig config)
    {
        _clientId = config.ApsClientId;
        _clientSecret = config.ApsClientSecret;
        _baseUrl = config.ApsBaseUrl;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Returns true if APS credentials are configured.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrEmpty(_clientId) && !string.IsNullOrEmpty(_clientSecret);

    /// <summary>
    /// Gets a valid access token, refreshing if needed.
    /// </summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
                return _cachedToken;
        }

        var token = await AcquireTokenAsync(ct);

        lock (_lock)
        {
            _cachedToken = token.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
        }

        return token.AccessToken;
    }

    private async Task<(string AccessToken, int ExpiresIn)> AcquireTokenAsync(CancellationToken ct)
    {
        var url = $"{_baseUrl}/authentication/v2/token";
        LogService.Info("ApsAuthService: Acquiring access token.");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret),
            new KeyValuePair<string, string>("scope", "data:read viewables:read")
        });

        var response = await _httpClient.PostAsync(url, content, ct);
#if NETFRAMEWORK
        var body = await response.Content.ReadAsStringAsync();
#else
        var body = await response.Content.ReadAsStringAsync(ct);
#endif

        if (!response.IsSuccessStatusCode)
        {
            LogService.Error($"ApsAuthService: Token acquisition failed with status {(int)response.StatusCode}.");
            throw new InvalidOperationException($"APS authentication failed: {(int)response.StatusCode} — {body}");
        }

        var json = JObject.Parse(body);
        var accessToken = json["access_token"]?.ToString()
            ?? throw new InvalidOperationException("APS response missing access_token.");
        var expiresIn = json["expires_in"]?.Value<int>() ?? 3600;

        LogService.Info("ApsAuthService: Token acquired successfully.");
        return (accessToken, expiresIn);
    }
}
