namespace Modena.RevitAddin.Updater;

/// <summary>
/// Lightweight file + debug logger for the Updater process.
/// Self-contained so the Updater has no dependency on the main add-in project.
/// </summary>
internal static class UpdaterLogger
{
    private static readonly object Lock = new();
    private static string? _logPath;

    internal static void Initialize(string appName)
    {
        try
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                appName, "logs");
            Directory.CreateDirectory(logDir);
            _logPath = Path.Combine(logDir, $"{DateTime.Now:yyyy-MM-dd}.log");
        }
        catch { /* ignore */ }
    }

    internal static void Log(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        System.Diagnostics.Debug.WriteLine(line);

        if (_logPath is null) return;
        try { lock (Lock) { File.AppendAllText(_logPath, line + Environment.NewLine); } }
        catch { /* swallow */ }
    }
}
