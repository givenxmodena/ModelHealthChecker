using System.IO;
using Newtonsoft.Json;

namespace Modena.RevitAddin.Services;

/// <summary>Thresholds and enabled flag for a single health check rule.</summary>
public class HealthCheckRule
{
    public bool    Enabled         { get; set; } = true;
    public int     HighThreshold   { get; set; }
    public int     MediumThreshold { get; set; }

    /// <summary>Optional discipline override (e.g. "Architecture", "MEP", "Structure"). Null means use the check's built-in default.</summary>
    public string? Discipline      { get; set; }

    internal string ResolvePriority(int count) =>
        count > HighThreshold ? "HIGH" : count > MediumThreshold ? "MEDIUM" : "LOW";
}

/// <summary>Per-check rule set loaded from modena-config.json.</summary>
public class HealthChecksConfig
{
    public HealthCheckRule MirroredElements     { get; set; } = new() { HighThreshold = 50,   MediumThreshold = 10   };
    public HealthCheckRule UnplacedRooms        { get; set; } = new() { HighThreshold = 10,   MediumThreshold = 1    };
    public HealthCheckRule DuplicateTypeNames   { get; set; } = new() { HighThreshold = 20,   MediumThreshold = 5    };
    public HealthCheckRule DetailLineItems      { get; set; } = new() { HighThreshold = 5000, MediumThreshold = 1000 };
    public HealthCheckRule ImportedCadInstances { get; set; } = new() { HighThreshold = 10,   MediumThreshold = 3    };
}

/// <summary>
/// Reads add-in configuration from a JSON settings file.
/// Looks for modena-config.json next to the add-in assembly.
/// </summary>
public class PluginConfig
{
    /// <summary>Warnings accumulated during Load/Validate — exposed to the UI via the ViewModel.</summary>
    public List<string> ValidationWarnings { get; } = new();

    public int RefreshIntervalMinutes { get; set; } = 30;
    public bool AutoRefreshEnabled { get; set; } = true;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public string LoggingLevel { get; set; } = "Information";

    /// <summary>Minutes before family data is considered stale and re-fetched on re-analysis.</summary>
    public int FamilyCacheMinutes { get; set; } = 10;

    /// <summary>Per-check thresholds and enabled flags. Overrides built-in defaults when present in config file.</summary>
    public HealthChecksConfig HealthChecks { get; set; } = new();

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
            var cfg = new PluginConfig();
            cfg.ValidationWarnings.Add($"Configuration file not found at '{System.IO.Path.GetFileName(path)}'. Using built-in defaults.");
            return cfg;
        }

        try
        {
            var json    = File.ReadAllText(path);
            var config  = JsonConvert.DeserializeObject<PluginConfig>(json) ?? new PluginConfig();
            LogService.Info($"Loaded config from '{path}'.");
            config.Validate();
            return config;
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to read config from '{path}', using defaults.", ex);
            var cfg = new PluginConfig();
            cfg.ValidationWarnings.Add($"Could not read '{System.IO.Path.GetFileName(path)}': {ex.Message}. Using built-in defaults.");
            return cfg;
        }
    }

    /// <summary>
    /// Clamps all values to safe bounds and resets any unrecognised strings to defaults.
    /// Logs and records a warning for each correction so both the UI and admins can see what was wrong.
    /// </summary>
    private void Validate()
    {
        if (RefreshIntervalMinutes < 1 || RefreshIntervalMinutes > 60)
        {
            RecordWarning($"RefreshIntervalMinutes={RefreshIntervalMinutes} out of range [1,60]; reset to 30.");
            RefreshIntervalMinutes = 30;
        }

        if (RequestTimeoutSeconds < 5 || RequestTimeoutSeconds > 300)
        {
            RecordWarning($"RequestTimeoutSeconds={RequestTimeoutSeconds} out of range [5,300]; reset to 30.");
            RequestTimeoutSeconds = 30;
        }

        if (FamilyCacheMinutes < 0 || FamilyCacheMinutes > 120)
        {
            RecordWarning($"FamilyCacheMinutes={FamilyCacheMinutes} out of range [0,120]; reset to 10.");
            FamilyCacheMinutes = 10;
        }

        var validLevels = new[] { "debug", "information", "warning", "error" };
        if (!validLevels.Contains((LoggingLevel ?? string.Empty).ToLowerInvariant()))
        {
            RecordWarning($"LoggingLevel='{LoggingLevel}' is not recognised; reset to 'Information'.");
            LoggingLevel = "Information";
        }

        if (!string.IsNullOrEmpty(ApsBaseUrl))
        {
            var valid = Uri.TryCreate(ApsBaseUrl, UriKind.Absolute, out var uri)
                     && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
            if (!valid)
            {
                RecordWarning($"ApsBaseUrl='{ApsBaseUrl}' is not a valid URL; reset to default.");
                ApsBaseUrl = "https://developer.api.autodesk.com";
            }
        }

        HealthChecks ??= new HealthChecksConfig();
        ValidateRule(nameof(HealthChecks.MirroredElements),     HealthChecks.MirroredElements,     50,   10);
        ValidateRule(nameof(HealthChecks.UnplacedRooms),        HealthChecks.UnplacedRooms,        10,    1);
        ValidateRule(nameof(HealthChecks.DuplicateTypeNames),   HealthChecks.DuplicateTypeNames,   20,    5);
        ValidateRule(nameof(HealthChecks.DetailLineItems),      HealthChecks.DetailLineItems,      5000, 1000);
        ValidateRule(nameof(HealthChecks.ImportedCadInstances), HealthChecks.ImportedCadInstances, 10,    3);
    }

    private void ValidateRule(string name, HealthCheckRule rule, int defaultHigh, int defaultMedium)
    {
        if (rule.HighThreshold < 0)
        {
            RecordWarning($"HealthChecks.{name}.HighThreshold={rule.HighThreshold} is negative; reset to {defaultHigh}.");
            rule.HighThreshold = defaultHigh;
        }
        if (rule.MediumThreshold < 0)
        {
            RecordWarning($"HealthChecks.{name}.MediumThreshold={rule.MediumThreshold} is negative; reset to {defaultMedium}.");
            rule.MediumThreshold = defaultMedium;
        }
        if (rule.MediumThreshold > rule.HighThreshold)
        {
            RecordWarning($"HealthChecks.{name}: MediumThreshold ({rule.MediumThreshold}) > HighThreshold ({rule.HighThreshold}); swapping.");
            (rule.HighThreshold, rule.MediumThreshold) = (rule.MediumThreshold, rule.HighThreshold);
        }
    }

    private void RecordWarning(string message)
    {
        LogService.Warn($"Config: {message}");
        ValidationWarnings.Add(message);
    }

    private static string GetDefaultConfigPath()
    {
        var assemblyDir = Path.GetDirectoryName(
            typeof(PluginConfig).Assembly.Location) ?? string.Empty;
        return Path.Combine(assemblyDir, "modena-config.json");
    }
}
