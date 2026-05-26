using System.IO;
using Modena.Shared.DTOs;
using Newtonsoft.Json;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Persists the last successful DashboardResponse per model to AppData as JSON.
/// On next open the ViewModel restores this instantly while a background refresh runs.
/// </summary>
public class DashboardCacheService
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Modena", "cache");

    public void Save(string modelKey, DashboardResponse data)
    {
        if (string.IsNullOrEmpty(modelKey)) return;
        try
        {
            Directory.CreateDirectory(CacheDir);
            var json = JsonConvert.SerializeObject(data, Formatting.None);
            File.WriteAllText(GetPath(modelKey), json, System.Text.Encoding.UTF8);
            LogService.Info($"Cache: Saved snapshot for '{modelKey}'.");
        }
        catch (Exception ex)
        {
            LogService.Warn($"Cache: Failed to save snapshot. {ex.Message}");
        }
    }

    public DashboardResponse? TryLoad(string modelKey)
    {
        if (string.IsNullOrEmpty(modelKey)) return null;
        try
        {
            var path = GetPath(modelKey);
            if (!File.Exists(path)) return null;
            var json  = File.ReadAllText(path, System.Text.Encoding.UTF8);
            var data  = JsonConvert.DeserializeObject<DashboardResponse>(json);
            if (data is null) return null;
            LogService.Info($"Cache: Loaded snapshot for '{modelKey}' (saved {data.LastUpdatedUtc:HH:mm d MMM} UTC).");
            return data;
        }
        catch (Exception ex)
        {
            LogService.Warn($"Cache: Failed to load snapshot. {ex.Message}");
            return null;
        }
    }

    private static string GetPath(string modelKey) =>
        Path.Combine(CacheDir, $"{Sanitize(modelKey)}.json");

    private static string Sanitize(string key)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(key.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
