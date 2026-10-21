using System.IO;
using System.Security.Cryptography;
using System.Text;
using Modena.Shared.DTOs;
using Newtonsoft.Json;

namespace Modena.RevitAddin.Services;

/// <summary>Per-user append-only history. Call from a worker thread.</summary>
public sealed class HealthHistoryService
{
    private readonly string _directory;

    public HealthHistoryService(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Modena", "history");
    }

    public bool Append(string modelKey, HealthScanRecord record)
    {
        if (string.IsNullOrWhiteSpace(modelKey)) return false;
        try
        {
            if (record.ModelKey != modelKey)
                throw new ArgumentException("The scan does not belong to this model.");
            var line = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(record, Formatting.None) + "\n");
            WithLock(modelKey, path =>
            {
                Directory.CreateDirectory(_directory);
                using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                // A crash may leave a partial last line. Isolate it; never overwrite history.
                if (stream.Length > 0)
                {
                    stream.Seek(-1, SeekOrigin.End);
                    if (stream.ReadByte() != '\n') stream.WriteByte((byte)'\n');
                }
                stream.Seek(0, SeekOrigin.End);
                stream.Write(line, 0, line.Length);
                stream.Flush();
                return true;
            });
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                                   || ex is ArgumentException || ex is JsonException || ex is TimeoutException)
        {
            LogService.Warn($"History: Could not save scan. {ex.Message}");
            return false;
        }
    }

    public List<HealthScanRecord> LoadHistory(string modelKey)
    {
        if (string.IsNullOrWhiteSpace(modelKey)) return new List<HealthScanRecord>();
        return WithLock(modelKey, path =>
        {
            var records = new List<HealthScanRecord>();
            if (!File.Exists(path)) return records;
            var skipped = 0;
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var record = JsonConvert.DeserializeObject<HealthScanRecord>(line);
                    if (record is null || record.SchemaVersion != 1 || record.ModelKey != modelKey
                        || record.Timestamp == default || record.Timestamp.Kind != DateTimeKind.Utc
                        || string.IsNullOrWhiteSpace(record.Id) || record.PassRate < 0 || record.PassRate > 100
                        || record.FailedCheckSummaries is null || record.PassedCheckNames is null
                        || record.FailedCheckSummaries.Any(c => c is null || string.IsNullOrWhiteSpace(c.Name) || c.Count < 0)
                        || record.PassedCheckNames.Any(string.IsNullOrWhiteSpace))
                    {
                        skipped++;
                        continue;
                    }
                    records.Add(record);
                }
                catch (JsonException) { skipped++; }
            }
            if (skipped > 0) LogService.Warn($"History: Skipped {skipped} invalid or unsupported record(s).");
            return records.OrderBy(r => r.Timestamp).ToList();
        });
    }

    private T WithLock<T>(string modelKey, Func<string, T> action)
    {
        var safePrefix = new string(modelKey.Where(char.IsLetterOrDigit).Take(32).ToArray());
        var path = Path.Combine(_directory, safePrefix + "-" + Hash(modelKey) + ".jsonl");
        using var mutex = new Mutex(false, "Local\\ModenaHealthHistory-" + Hash(Path.GetFullPath(path).ToUpperInvariant()));
        var acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new TimeoutException("History is busy in another Revit session. Try again.");
            return action(path);
        }
        finally { if (acquired) mutex.ReleaseMutex(); }
    }

    private static string Hash(string value)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
    }
}
