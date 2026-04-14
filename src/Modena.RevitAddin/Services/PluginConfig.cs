using System.IO;
using Newtonsoft.Json;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Reads add-in configuration from a JSON settings file.
/// Looks for modena-config.json next to the add-in assembly.
/// </summary>
public class PluginConfig
{
    public int RefreshIntervalMinutes { get; set; } = 5;
    public bool AutoRefreshEnabled { get; set; } = true;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public string LoggingLevel { get; set; } = "Information";

    // APS credentials for cloud model support
    public string ApsClientId { get; set; } = string.Empty;
    public string ApsClientSecret { get; set; } = string.Empty;
    public string ApsBaseUrl { get; set; } = "https://developer.api.autodesk.com";

    /// <summary>
    /// Loads configuration from the specified JSON file path.
    /// Returns default values if the file does not exist or cannot be parsed.
    /// </summary>
    public static PluginConfig Load(string? configFilePath = null)
    {
        var path = configFilePath ?? GetDefaultConfigPath();

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            LogService.Info($"Config file not found at '{path}', using defaults.");
            return new PluginConfig();
        }

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonConvert.DeserializeObject<PluginConfig>(json);
            LogService.Info($"Loaded config from '{path}'.");
            return config ?? new PluginConfig();
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to read config from '{path}', using defaults.", ex);
            return new PluginConfig();
        }
    }

    private static string GetDefaultConfigPath()
    {
        var assemblyDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(assemblyDir, "modena-config.json");
    }
}
