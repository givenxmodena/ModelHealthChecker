using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Modena.Shared.DTOs;
using Newtonsoft.Json.Linq;

namespace Modena.RevitAddin.Services;

public interface ICloudModelService
{
    Task<CloudModelInfo> LoadAsync(ModelIdentity identity, bool allowSignIn,
        Action<string> status, CancellationToken ct);
}

public sealed class CloudModelInfo
{
    public string ItemId { get; set; } = string.Empty;
    public string VersionUrn { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
    public int? ObjectCount { get; set; }
}

public sealed class CloudModelException : Exception
{
    public CloudModelException(string message, CloudModelInfo? identity = null) : base(message) => Identity = identity;
    public CloudModelInfo? Identity { get; }
}

/// <summary>Read-only APS enrichment. Published cloud data never replaces live Revit check results.</summary>
public sealed class ApsCloudModelService : ICloudModelService
{
    private static readonly Uri Origin = new("https://developer.api.autodesk.com/");
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = Timeout.InfiniteTimeSpan };
    private readonly HttpClient _http;
    private readonly Func<bool, CancellationToken, Task<string>> _getToken;
    private readonly TimeSpan _timeout;

    public ApsCloudModelService(PluginConfig config)
    {
        _http = Http;
        _timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds);
        _getToken = async (interactive, ct) =>
        {
            var (clientId, clientSecret) = ApsCredentialsProvider.GetCredentials();
            if (string.IsNullOrWhiteSpace(clientId)) clientId = config.ApsClientId;
            if (string.IsNullOrWhiteSpace(clientSecret)) clientSecret = config.ApsClientSecret;
            if (string.IsNullOrWhiteSpace(clientId))
                throw new CloudModelException("APS credentials are not configured.");
            return await new AuthService(clientId!, clientSecret ?? "")
                .GetCloudAccessTokenAsync(interactive, ct);
        };
    }

    public ApsCloudModelService(HttpClient http, Func<bool, CancellationToken, Task<string>> getToken, TimeSpan timeout)
    {
        _http = http;
        _getToken = getToken;
        _timeout = timeout;
    }

    public async Task<CloudModelInfo> LoadAsync(ModelIdentity identity, bool allowSignIn,
        Action<string> status, CancellationToken ct)
    {
        if (!identity.IsCloudModel) throw new CloudModelException("The active model is not an ACC cloud model.");
        if (!Guid.TryParse(identity.ProjectGuid, out var projectGuid) || !Guid.TryParse(identity.ModelGuid, out var modelGuid))
            throw new CloudModelException("Cloud project/model identity is unavailable.");
        // User interaction can take longer than a network request; closing Pulse still cancels it.
        status("Authenticating…");
        var token = await _getToken(allowSignIn, ct);
        if (string.IsNullOrWhiteSpace(token)) throw new CloudModelException("Autodesk sign-in is required.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_timeout);
        var requestCt = timeout.Token;
        status("Loading published cloud data…");
        var project = "b." + projectGuid.ToString("D");
        JObject version;
        if (!string.IsNullOrWhiteSpace(identity.ItemId))
        {
            var tip = await GetAsync($"data/v1/projects/{E(project)}/items/{E(identity.ItemId!)}/tip", token, requestCt);
            version = tip["data"] as JObject ?? throw new CloudModelException("ACC version metadata is unavailable.");
            if (!Matches(version, projectGuid, modelGuid))
                throw new CloudModelException("The ACC item does not match the open model.");
        }
        else
        {
            string? root = null;
            var hubs = await PagesAsync("project/v1/hubs", token, requestCt);
            foreach (var hub in hubs)
            {
                if (!(hub.Value<string>("id") ?? "").StartsWith("b.", StringComparison.Ordinal)) continue;
                try
                {
                    var projectData = await GetAsync($"project/v1/hubs/{E(hub.Value<string>("id")!)}/projects/{E(project)}", token, requestCt, true);
                    root = projectData.SelectToken("data.relationships.rootFolder.data.id")?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(root)) break;
                }
                catch (CloudModelException ex) when (ex.Message == "Project is not accessible in this hub.") { }
            }
            if (string.IsNullOrWhiteSpace(root)) throw new CloudModelException("The ACC project is not accessible to this Autodesk user/app.");
            var versions = await PagesAsync($"data/v1/projects/{E(project)}/folders/{E(root!)}/search?filter[extension.type]=versions:autodesk.bim360:C4RModel", token, requestCt);
            var matches = versions.Where(v => Matches(v, projectGuid, modelGuid)).ToList();
            if (matches.Count != 1)
                throw new CloudModelException(matches.Count == 0 ? "No published ACC version matches the open model." : "More than one ACC item matches the open model.");
            version = matches[0];
        }

        var info = new CloudModelInfo
        {
            ItemId = version.SelectToken("relationships.item.data.id")?.Value<string>() ?? identity.ItemId ?? "",
            VersionUrn = version.Value<string>("id") ?? ""
        };
        if (info.ItemId.Length == 0 || info.VersionUrn.Length == 0)
            throw new CloudModelException("ACC item/version identity is incomplete.");
        try
        {
            var derivative = version.SelectToken("relationships.derivatives.data.id")?.Value<string>();
            if (string.IsNullOrWhiteSpace(derivative)) throw new CloudModelException("The published model has no available derivative.");
            var metadata = await GetAsync($"modelderivative/v2/designdata/{E(derivative!)}/metadata", token, requestCt);
            var views = (metadata.SelectToken("data.metadata") as JArray)?.OfType<JObject>().ToList() ?? new List<JObject>();
            var view = views.FirstOrDefault(v => v.Value<string>("role") == "3d") ?? views.FirstOrDefault();
            var guid = view?.Value<string>("guid");
            if (string.IsNullOrWhiteSpace(guid)) throw new CloudModelException("The published model has no available property view.");
            var properties = await GetAsync($"modelderivative/v2/designdata/{E(derivative!)}/metadata/{E(guid!)}/properties", token, requestCt);
            var collection = properties.SelectToken("data.collection") as JArray
                ?? throw new CloudModelException("Published model properties are not available yet.");
            info.ViewName = view!.Value<string>("name") ?? "Model view";
            info.ObjectCount = collection.Count;
            return info;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (CloudModelException ex) { throw new CloudModelException(ex.Message, info); }
        catch (OperationCanceledException) { throw new CloudModelException("The cloud request timed out.", info); }
        catch (Exception) { throw new CloudModelException("Published cloud properties could not be read.", info); }
    }

    private static bool Matches(JObject version, Guid project, Guid model)
    {
        var data = version.SelectToken("attributes.extension.data");
        return Guid.TryParse(data?["modelGuid"]?.Value<string>(), out var foundModel) && foundModel == model
            && (data?["projectGuid"] is null || Guid.TryParse(data["projectGuid"]?.Value<string>(), out var foundProject) && foundProject == project);
    }

    private async Task<List<JObject>> PagesAsync(string path, string token, CancellationToken ct)
    {
        var result = new List<JObject>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var page = 0; page < 50; page++)
        {
            if (!seen.Add(path)) throw new CloudModelException("ACC returned a repeated results page.");
            var json = await GetAsync(path, token, ct);
            var rows = json["data"] as JArray ?? throw new CloudModelException("ACC results were incomplete.");
            result.AddRange(rows.OfType<JObject>());
            if (result.Count > 20000) throw new CloudModelException("ACC search exceeded the supported result limit.");
            var next = json.SelectToken("links.next.href")?.Value<string>();
            if (string.IsNullOrWhiteSpace(next)) return result;
            path = next!;
        }
        throw new CloudModelException("ACC search exceeded the supported page limit.");
    }

    private async Task<JObject> GetAsync(string path, string token, CancellationToken ct, bool hubProbe = false)
    {
        var uri = new Uri(Origin, path);
        if (uri.Scheme != Uri.UriSchemeHttps || uri.Host != Origin.Host || uri.Port != Origin.Port || uri.UserInfo.Length != 0
            || !(uri.AbsolutePath.StartsWith("/project/v1/", StringComparison.Ordinal)
                 || uri.AbsolutePath.StartsWith("/data/v1/", StringComparison.Ordinal)
                 || uri.AbsolutePath.StartsWith("/modelderivative/v2/", StringComparison.Ordinal)))
            throw new CloudModelException("ACC returned an untrusted results URL.");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (hubProbe && (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound))
            throw new CloudModelException("Project is not accessible in this hub.");
        if (response.StatusCode == HttpStatusCode.Accepted)
            throw new CloudModelException("Published model properties are still processing. Try Refresh later.");
        if (!response.IsSuccessStatusCode)
            throw new CloudModelException($"APS request failed (HTTP {(int)response.StatusCode}).");
        const int maxBytes = 20 * 1024 * 1024;
        using var input = await response.Content.ReadAsStreamAsync();
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(bytes, 0, bytes.Length, ct)) != 0)
        {
            if (buffer.Length + read > maxBytes) throw new CloudModelException("APS response exceeded the supported size limit.");
            buffer.Write(bytes, 0, read);
        }
        var body = Encoding.UTF8.GetString(buffer.ToArray());
        return await Task.Run(() => JObject.Parse(body), ct);
    }

    private static string E(string value) => Uri.EscapeDataString(value);
}
