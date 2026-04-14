using System.Diagnostics;
using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace Modena.RevitAddin.Updater;

/// <summary>
/// Launcher / Updater for the Modena Model Health Checker Revit add-in.
///
/// ARCHITECTURE:
/// - Velopack manages this Launcher in %LocalAppData%\ModelHealthChecker\
/// - This Launcher deploys the add-in DLLs to each installed Revit version
/// - The Revit add-in triggers this Launcher with --update when the user requests an update
///
/// MULTI-TARGETING:
/// - Revit 2025-2026: .NET 8.0  (deploy from net8.0-windows/ subfolder)
/// - Revit 2024:      .NET 4.8  (deploy from net48/ subfolder)
///
/// Usage:
///   Modena.RevitAddin.Updater.exe                                          # Deploy only
///   Modena.RevitAddin.Updater.exe --update                                 # Check for updates, apply, deploy
///   Modena.RevitAddin.Updater.exe --update --revit-pid 1234 --revit-exe "C:\...\Revit.exe"
///   Modena.RevitAddin.Updater.exe --silent                                 # Headless mode
/// </summary>
class Program
{
    private const string GitHubRepoUrl = "https://github.com/givenxmodena/ModelHealthChecker";
    private const string AppName = "ModelHealthChecker";

    private static readonly Dictionary<string, string> RevitFrameworkMap = new()
    {
        { "2024", "net48" },
        { "2025", "net8.0-windows" },
        { "2026", "net8.0-windows" }
    };

    [STAThread]
    public static int Main(string[] args)
    {
        bool silent = args.Contains("--silent");
        bool updateMode = args.Contains("--update");

        int? revitPid = GetArgInt(args, "--revit-pid");
        string? revitExePath = GetArgString(args, "--revit-exe");

        string argsJoined = string.Join(" ", args ?? []);
        string version = GetAssemblyVersion();

        UpdaterLogger.Initialize("Modena.RevitAddin.Updater");
        UpdaterLogger.Log($"Starting updater v{version}. Args=\"{argsJoined}\"");

        try
        {
            try { VelopackApp.Build().Run(); }
            catch (Exception ex) { UpdaterLogger.Log($"Velopack bootstrap warning: {ex.Message}"); }

            if (silent)
                return RunSilentAsync(updateMode, revitPid, revitExePath).GetAwaiter().GetResult();

            var app = new System.Windows.Application();
            var window = new UpdateWindow(updateMode, revitPid, revitExePath);
            app.Run(window);
            return window.ExitCode;
        }
        catch (Exception ex)
        {
            UpdaterLogger.Log($"FATAL: {ex}");
            return 1;
        }
    }

    internal static async Task<int> RunSilentAsync(bool updateMode, int? revitPid, string? revitExePath)
    {
        try
        {
            if (revitPid.HasValue)
                await WaitForRevitToClose(revitPid.Value);

            if (updateMode)
            {
                bool updated = await CheckAndApplyUpdatesAsync(revitPid, revitExePath);
                if (updated) return 0; // Velopack restarts the process
                UpdaterLogger.Log("No updates available — you are on the latest version.");
            }

            DeployToRevit();
            UpdaterLogger.Log("Deployment complete.");

            if (!string.IsNullOrEmpty(revitExePath))
                RestartRevit(revitExePath);
            else
                UpdaterLogger.Log("Restart Revit to use the updated add-in.");

            return 0;
        }
        catch (Exception ex)
        {
            UpdaterLogger.Log($"Error in RunSilentAsync: {ex}");
            return 1;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Update / Deploy helpers (called from both silent and GUI flows)
    // ─────────────────────────────────────────────────────────────────────────

    internal static async Task<bool> CheckAndApplyUpdatesAsync(
        int? revitPid = null,
        string? revitExePath = null,
        Action<string>? statusCallback = null,
        Action<int>? progressCallback = null)
    {
        try
        {
            statusCallback?.Invoke("Checking for updates...");
            UpdaterLogger.Log("Checking for updates...");

            var source = new GithubSource(GitHubRepoUrl, null, false);
            var mgr = new UpdateManager(source);

            var info = await mgr.CheckForUpdatesAsync();
            if (info?.TargetFullRelease == null)
            {
                statusCallback?.Invoke("Already up to date.");
                return false;
            }

            string msg = $"Found update: v{info.TargetFullRelease.Version}";
            statusCallback?.Invoke(msg);
            UpdaterLogger.Log(msg);

            statusCallback?.Invoke("Downloading update...");
            UpdaterLogger.Log("Downloading update...");

            await mgr.DownloadUpdatesAsync(info, p =>
            {
                progressCallback?.Invoke(p);
                if (p % 20 == 0) UpdaterLogger.Log($"  Download: {p}%");
            });

            statusCallback?.Invoke("Applying update...");
            UpdaterLogger.Log("Applying update and restarting...");
            mgr.ApplyUpdatesAndRestart(info.TargetFullRelease);

            return true;
        }
        catch (Exception ex)
        {
            UpdaterLogger.Log($"Update failed: {ex}");
            throw;
        }
    }

    internal static void DeployToRevit()
    {
        UpdaterLogger.Log("Deploying add-in to Revit...");

        string appDir = AppContext.BaseDirectory;
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // DLL names that differ per framework
        string[] net8Files =
        [
            "Modena.RevitAddin.dll",
            "Modena.RevitAddin.deps.json",
            "Modena.Shared.dll",
            "Newtonsoft.Json.dll",
            "Velopack.dll",
            "NuGet.Versioning.dll"
        ];

        string[] net48Files =
        [
            "Modena.RevitAddin.dll",
            "Modena.Shared.dll",
            "Newtonsoft.Json.dll"
        ];

        foreach (var (revitVersion, framework) in RevitFrameworkMap)
        {
            if (!IsRevitInstalled(revitVersion))
            {
                UpdaterLogger.Log($"Skipping Revit {revitVersion} (not installed).");
                continue;
            }

            try
            {
                string addInsRoot = Path.Combine(appData, "Autodesk", "Revit", "Addins", revitVersion);
                Directory.CreateDirectory(addInsRoot);

                string addinDir = Path.Combine(addInsRoot, AppName);
                Directory.CreateDirectory(addinDir);

                string sourceDir = Path.Combine(appDir, framework);
                if (!Directory.Exists(sourceDir))
                {
                    UpdaterLogger.Log($"  Falling back to root dir for Revit {revitVersion}.");
                    sourceDir = appDir;
                }

                string[] filesToCopy = framework == "net8.0-windows" ? net8Files : net48Files;

                foreach (string file in filesToCopy)
                {
                    string src = Path.Combine(sourceDir, file);
                    string dst = Path.Combine(addinDir, file);
                    if (File.Exists(src))
                    {
                        CopyWithRetry(src, dst, revitVersion);
                        UpdaterLogger.Log($"  Copied {file} → Revit {revitVersion}");
                    }
                    else
                    {
                        UpdaterLogger.Log($"  Warning: {file} not found in {sourceDir}");
                    }
                }

                // Copy Registry/ folder
                string regSrc = Path.Combine(sourceDir, "Registry");
                if (Directory.Exists(regSrc))
                {
                    string regDst = Path.Combine(addinDir, "Registry");
                    Directory.CreateDirectory(regDst);
                    foreach (string regFile in Directory.GetFiles(regSrc))
                    {
                        string dest = Path.Combine(regDst, Path.GetFileName(regFile));
                        File.Copy(regFile, dest, overwrite: true);
                    }
                    UpdaterLogger.Log($"  Copied Registry/ → Revit {revitVersion}");
                }

                // Copy modena-config.json
                string cfgSrc = Path.Combine(sourceDir, "modena-config.json");
                if (File.Exists(cfgSrc))
                {
                    File.Copy(cfgSrc, Path.Combine(addinDir, "modena-config.json"), overwrite: true);
                    UpdaterLogger.Log($"  Copied modena-config.json → Revit {revitVersion}");
                }

                // Generate .addin manifest from template
                string templatePath = Path.Combine(appDir, "Modena.RevitAddin.addin.template");
                if (File.Exists(templatePath))
                {
                    string content = File.ReadAllText(templatePath).Replace("{APPDIR}", addinDir);
                    File.WriteAllText(Path.Combine(addInsRoot, "Modena.RevitAddin.addin"), content);
                    UpdaterLogger.Log($"  Generated .addin manifest for Revit {revitVersion}");
                }

                UpdaterLogger.Log($"Deployed to Revit {revitVersion} ({framework}).");
            }
            catch (Exception ex)
            {
                UpdaterLogger.Log($"Failed to deploy to Revit {revitVersion}: {ex.Message}");
            }
        }
    }

    internal static async Task WaitForRevitToClose(int revitPid)
    {
        UpdaterLogger.Log($"Waiting for Revit (PID {revitPid}) to close...");

        try
        {
            var proc = Process.GetProcessById(revitPid);
            int waited = 0;
            while (!proc.HasExited && waited < 60_000)
            {
                await Task.Delay(1_000);
                waited += 1_000;
            }
            UpdaterLogger.Log(proc.HasExited ? "Revit closed." : "Timed out waiting for Revit.");
        }
        catch (ArgumentException) { UpdaterLogger.Log("Revit already closed."); }

        await Task.Delay(2_000);
    }

    internal static void RestartRevit(string revitExePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = revitExePath, UseShellExecute = true });
            UpdaterLogger.Log("Revit restarted.");
        }
        catch (Exception ex) { UpdaterLogger.Log($"Failed to restart Revit: {ex.Message}"); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static bool IsRevitInstalled(string version)
    {
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return File.Exists(Path.Combine(pf, "Autodesk", $"Revit {version}", "Revit.exe"))
            || File.Exists(Path.Combine(pf86, "Autodesk", $"Revit {version}", "Revit.exe"));
    }

    private static void CopyWithRetry(string src, string dst, string revitVersion)
    {
        const int maxAttempts = 6;
        const int delay = 5_000;

        for (int i = 1; i <= maxAttempts; i++)
        {
            try { File.Copy(src, dst, overwrite: true); return; }
            catch (IOException) when (i < maxAttempts)
            {
                UpdaterLogger.Log($"  File locked for Revit {revitVersion}, retrying ({i}/{maxAttempts})...");
                Thread.Sleep(delay);
            }
        }
        File.Copy(src, dst, overwrite: true);
    }

    private static string? GetArgString(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static int? GetArgInt(string[] args, string name)
    {
        var val = GetArgString(args, name);
        return val is not null && int.TryParse(val, out int n) ? n : null;
    }

    private static string GetAssemblyVersion()
    {
        try { return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0"; }
        catch { return "1.0.0.0"; }
    }
}
