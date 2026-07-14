using System.IO;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace Modena.RevitAddin.Services;

/// <summary>
/// PostHog analytics — fire-and-forget event capture with offline queuing.
/// All calls are non-blocking; failures are silently queued and retried on next startup.
/// Disabled automatically when no API key is configured.
/// </summary>
public static class AnalyticsService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static string _apiKey       = string.Empty;
    private static string _distinctId   = "anonymous";
    private static string _appVersion   = "unknown";
    private static string _revitVersion = "unknown";
    private static bool   _enabled      = false;

    private const string Endpoint   = "https://us.i.posthog.com/capture/";
    private const int    MaxQueue   = 100;

    private static readonly string QueuePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Modena", "ModelHealthChecker", "analytics_queue.json");

    private static readonly object QueueLock = new();

    public static void Initialize(string apiKey, string? appVersion = null, string? revitVersion = null)
    {
        _apiKey  = apiKey ?? string.Empty;
        _enabled = !string.IsNullOrWhiteSpace(_apiKey);

        if (!_enabled)
        {
            LogService.Info("AnalyticsService: No API key configured — analytics disabled.");
            return;
        }

        _appVersion   = appVersion   ?? typeof(AnalyticsService).Assembly.GetName().Version?.ToString() ?? "unknown";
        _revitVersion = revitVersion ?? "unknown";
        _distinctId   = GetOrCreateAnonymousId();

        Task.Run(FlushQueueAsync);
        LogService.Info($"AnalyticsService: Initialized. DistinctId={_distinctId}, AppVersion={_appVersion}");
    }

    /// <summary>
    /// Upgrades the anonymous ID to a real user identity and links prior activity.
    /// Called after successful APS authentication.
    /// </summary>
    public static void IdentifyUser(string email)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(email)) return;

        var previousId = _distinctId;
        _distinctId = email;

        // Link pre-login anonymous events to the identified user
        if (previousId.StartsWith("anon_", StringComparison.Ordinal))
        {
            FireAndForget(new Dictionary<string, object?>
            {
                ["api_key"]     = _apiKey,
                ["event"]       = "$create_alias",
                ["distinct_id"] = email,
                ["properties"]  = new Dictionary<string, object> { ["alias"] = previousId }
            });
        }

        FireAndForget(new Dictionary<string, object?>
        {
            ["api_key"]     = _apiKey,
            ["event"]       = "$identify",
            ["distinct_id"] = email,
            ["properties"]  = new Dictionary<string, object>
            {
                ["$set"] = new Dictionary<string, object>
                {
                    ["email"]        = email,
                    ["app_version"]  = _appVersion,
                    ["revit_version"] = _revitVersion,
                    ["os_version"]   = Environment.OSVersion.Version.ToString(),
                    ["platform"]     = "Revit"
                }
            }
        });

        LogService.Info($"AnalyticsService: User identified as '{email}'.");
    }

    public static void Capture(string eventName, Dictionary<string, object>? properties = null)
    {
        if (!_enabled) return;

        var props = new Dictionary<string, object>
        {
            ["app_version"]   = _appVersion,
            ["revit_version"] = _revitVersion,
            ["$lib"]          = "mhc-revit"
        };

        if (properties != null)
            foreach (var kv in properties)
                props[kv.Key] = kv.Value;

        FireAndForget(new Dictionary<string, object?>
        {
            ["api_key"]     = _apiKey,
            ["event"]       = eventName,
            ["distinct_id"] = _distinctId,
            ["properties"]  = props,
            ["timestamp"]   = DateTime.UtcNow.ToString("O")
        });
    }

    private static void FireAndForget(object payload)
    {
        Task.Run(async () =>
        {
            try
            {
                var json = JsonConvert.SerializeObject(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await Http.PostAsync(Endpoint, content).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    LogService.Info($"AnalyticsService: PostHog returned {(int)response.StatusCode}.");
            }
            catch (HttpRequestException)  { EnqueueEvent(payload); }
            catch (TaskCanceledException) { EnqueueEvent(payload); }
            catch (Exception ex)          { LogService.Info($"AnalyticsService: Capture failed silently — {ex.Message}"); }
        });
    }

    private static void EnqueueEvent(object payload)
    {
        try
        {
            var json = JsonConvert.SerializeObject(payload);
            lock (QueueLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(QueuePath)!);
                var queue = ReadQueue();
                if (queue.Count < MaxQueue)
                    queue.Add(json);
                File.WriteAllText(QueuePath, JsonConvert.SerializeObject(queue));
            }
        }
        catch (Exception ex)
        {
            LogService.Info($"AnalyticsService: Failed to queue event — {ex.Message}");
        }
    }

    private static async Task FlushQueueAsync()
    {
        List<string> toSend;
        lock (QueueLock)
        {
            toSend = ReadQueue();
            if (toSend.Count == 0) return;
            try { File.WriteAllText(QueuePath, "[]"); } catch { }
        }

        var failed      = new List<string>();
        var networkDown = false;

        foreach (var json in toSend)
        {
            if (networkDown) { failed.Add(json); continue; }
            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                await Http.PostAsync(Endpoint, content).ConfigureAwait(false);
            }
            catch (HttpRequestException) { networkDown = true; failed.Add(json); }
            catch (TaskCanceledException) { networkDown = true; failed.Add(json); }
            catch { /* corrupt event — skip */ }
        }

        if (failed.Count > 0)
        {
            lock (QueueLock)
            {
                try
                {
                    var current = ReadQueue();
                    var merged  = new List<string>(failed);
                    merged.AddRange(current);
                    if (merged.Count > MaxQueue) merged = merged.GetRange(0, MaxQueue);
                    File.WriteAllText(QueuePath, JsonConvert.SerializeObject(merged));
                }
                catch { }
            }
        }
    }

    private static List<string> ReadQueue()
    {
        try
        {
            if (!File.Exists(QueuePath)) return new List<string>();
            var text = File.ReadAllText(QueuePath);
            return JsonConvert.DeserializeObject<List<string>>(text) ?? new List<string>();
        }
        catch
        {
            try { File.Delete(QueuePath); } catch { }
            return new List<string>();
        }
    }

    private static string GetOrCreateAnonymousId()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Modena", "ModelHealthChecker", "anonymous_id.txt");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(existing)) return existing;
            }

            var id = $"anon_{Guid.NewGuid():N}";
            File.WriteAllText(path, id);
            return id;
        }
        catch
        {
            return $"anon_{Guid.NewGuid():N}";
        }
    }
}
