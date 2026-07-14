using System.IO;
using Newtonsoft.Json.Linq;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Provides APS Client ID/Secret at runtime without plain-text secrets in source.
///
/// Resolution order:
///   DEBUG  — env vars first, then packaged config files
///   RELEASE — packaged config files first, env vars as last-resort override
///
/// Config file search order (first match wins):
///   1. aps.json next to the add-in DLL
///   2. modena-config.json next to the add-in DLL  (reads ApsClientId / ApsClientSecret keys)
///   3. %AppData%\Autodesk\Revit\Addins\{2024|2025|2026}\aps.json
///   4. %AppData%\...\ModenaSuite\Modules\ModelHealthChecker\aps.json
///
/// aps.json supports plain keys (APS_CLIENT_ID / APS_CLIENT_SECRET) and
/// lightweight XOR-hex obfuscation (APS_CLIENT_ID_X / APS_CLIENT_SECRET_X).
/// </summary>
public static class ApsCredentialsProvider
{
    // XOR obfuscation key — must match the installer/obfuscation script.
    private const string XorKey = "M0d3nA-ExP-01";

    public static (string? ClientId, string? ClientSecret) GetCredentials()
    {
#if DEBUG
        var envId     = GetEnv("APS_CLIENT_ID");
        var envSecret = GetEnv("APS_CLIENT_SECRET");
        if (!string.IsNullOrWhiteSpace(envId) && !string.IsNullOrWhiteSpace(envSecret))
        {
            LogService.Info("ApsCredentialsProvider: Resolved from environment (DEBUG).");
            return (envId, envSecret);
        }

        foreach (var path in CandidatePaths())
        {
            var (id, sec) = TryLoad(path);
            if (id != null && sec != null) return (id, sec);
        }
#else
        foreach (var path in CandidatePaths())
        {
            var (id, sec) = TryLoad(path);
            if (id != null && sec != null) return (id, sec);
        }

        var envId     = GetEnv("APS_CLIENT_ID");
        var envSecret = GetEnv("APS_CLIENT_SECRET");
        if (!string.IsNullOrWhiteSpace(envId) && !string.IsNullOrWhiteSpace(envSecret))
        {
            LogService.Warn("ApsCredentialsProvider: Resolved from environment in RELEASE build.");
            return (envId, envSecret);
        }
#endif
        return (null, null);
    }

    private static string? GetEnv(string name) =>
        Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)
        ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
        ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);

    private static IEnumerable<string> CandidatePaths()
    {
        var baseDir  = AssemblyBaseDir();
        var appData  = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var addinsRoot = Path.Combine(appData, "Autodesk", "Revit", "Addins");

        // 1) Next to the DLL (both aps.json and modena-config.json)
        yield return Path.Combine(baseDir, "aps.json");
        yield return Path.Combine(baseDir, "modena-config.json");

        // 2) Revit Addins folders for supported versions
        foreach (var ver in new[] { "2024", "2025", "2026" })
        {
            yield return Path.Combine(addinsRoot, ver, "aps.json");
            yield return Path.Combine(addinsRoot, ver, "ModelHealthChecker", "aps.json");
            yield return Path.Combine(addinsRoot, ver, "ModenaSuite", "Modules", "ModelHealthChecker", "aps.json");
        }
    }

    private static (string? Id, string? Secret) TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path)) return (null, null);

            LogService.Info($"ApsCredentialsProvider: Probing {path}");
            var obj = JObject.Parse(File.ReadAllText(path));

            // Obfuscated format (aps.json)
            var idX  = obj.Value<string>("APS_CLIENT_ID_X");
            var secX = obj.Value<string>("APS_CLIENT_SECRET_X");
            if (!string.IsNullOrWhiteSpace(idX) && !string.IsNullOrWhiteSpace(secX))
            {
                var id  = DecodeXorHex(idX!);
                var sec = DecodeXorHex(secX!);
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(sec))
                {
                    LogService.Info($"ApsCredentialsProvider: Loaded (obfuscated) from {Path.GetFileName(path)}");
                    return (id, sec);
                }
            }

            // Plain env-style keys (aps.json)
            var plainId  = obj.Value<string>("APS_CLIENT_ID");
            var plainSec = obj.Value<string>("APS_CLIENT_SECRET");
            if (!string.IsNullOrWhiteSpace(plainId) && !string.IsNullOrWhiteSpace(plainSec))
            {
                LogService.Info($"ApsCredentialsProvider: Loaded (plain APS_ keys) from {Path.GetFileName(path)}");
                return (plainId, plainSec);
            }

            // MHC config style keys (modena-config.json)
            var cfgId  = obj.Value<string>("ApsClientId");
            var cfgSec = obj.Value<string>("ApsClientSecret");
            if (!string.IsNullOrWhiteSpace(cfgId) && !string.IsNullOrWhiteSpace(cfgSec))
            {
                LogService.Info($"ApsCredentialsProvider: Loaded (ApsClientId keys) from {Path.GetFileName(path)}");
                return (cfgId, cfgSec);
            }

            LogService.Info($"ApsCredentialsProvider: {Path.GetFileName(path)} found but has no recognised credential keys.");
            return (null, null);
        }
        catch (Exception ex)
        {
            LogService.Warn($"ApsCredentialsProvider: Failed to read '{path}' — {ex.Message}");
            return (null, null);
        }
    }

    private static string AssemblyBaseDir()
    {
        try
        {
            var loc = typeof(ApsCredentialsProvider).Assembly.Location;
            if (!string.IsNullOrWhiteSpace(loc))
            {
                var dir = Path.GetDirectoryName(loc);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir)) return dir!;
            }
        }
        catch { }

        try
        {
            var baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDir) && Directory.Exists(baseDir)) return baseDir!;
        }
        catch { }

        return Directory.GetCurrentDirectory();
    }

    private static string DecodeXorHex(string hex)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex) || hex.Length % 2 != 0) return string.Empty;
            var key = XorKey;
            var sb  = new System.Text.StringBuilder(hex.Length / 2);
            for (int i = 0; i < hex.Length; i += 2)
            {
                byte b  = Convert.ToByte(hex.Substring(i, 2), 16);
                char k  = key[(i / 2) % key.Length];
                sb.Append((char)(b ^ (byte)k));
            }
            return sb.ToString();
        }
        catch { return string.Empty; }
    }
}
