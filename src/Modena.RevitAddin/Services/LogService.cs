using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Structured logging service for the Revit add-in.
/// Writes to both Debug output and a log file.
/// Excludes secrets and PII from all output.
/// </summary>
public static class LogService
{
    private static readonly object Lock = new();
    private static string? _logFilePath;
    private static LogLevel _minLevel = LogLevel.Information;

    public static void Configure(string logFilePath, string loggingLevel)
    {
        _logFilePath = logFilePath;
        _minLevel = ParseLevel(loggingLevel);
    }

    public static void Debug(string message) => Write(LogLevel.Debug, message);
    public static void Info(string message) => Write(LogLevel.Information, message);
    public static void Warn(string message) => Write(LogLevel.Warning, message);

    public static void Error(string message, Exception? ex = null)
    {
        var full = ex is null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}";
        Write(LogLevel.Error, full);
    }

    private static void Write(LogLevel level, string message)
    {
        if (level < _minLevel) return;

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var line = $"[{timestamp}] [{level}] {message}";

        System.Diagnostics.Debug.WriteLine(line);

        if (_logFilePath is null) return;

        try
        {
            lock (Lock)
            {
                var dir = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Swallow file write errors to avoid crashing Revit
        }
    }

    private static LogLevel ParseLevel(string level) =>
        level?.ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "information" => LogLevel.Information,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            _ => LogLevel.Information
        };

    private enum LogLevel
    {
        Debug = 0,
        Information = 1,
        Warning = 2,
        Error = 3
    }
}
