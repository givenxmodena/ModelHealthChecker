// TRIAL VERSION CODE - excluded from production builds unless TRIAL_VERSION is defined.
#if TRIAL_VERSION
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Manages trial period functionality for Modena Model Health Checker.
/// </summary>
public static class TrialManager
{
    private static readonly string RegistryPath = @"SOFTWARE\ModenaAEC\ModelHealthChecker";
    private static readonly string InstallDateKey = "InstallDate";
    private static readonly string RefreshCountKey = "RefreshCount";
    private static readonly string TrialHashKey = "TrialHash";
    private static readonly string TrialVersionKey = "TrialVersion";

    private const int TRIAL_VERSION = 1;
    private const int TRIAL_DAYS = 60;
    private const int MAX_TRIAL_REFRESHES = 1000;

    /// <summary>
    /// Gets the current trial status.
    /// </summary>
    public static TrialStatus GetTrialStatus()
    {
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key is not null)
                    MigrateOldTrialIfNeeded(key);
            }

            var installDate = GetInstallDate();
            if (!IsTrialValid())
            {
                LogService.Warn("TrialManager: Trial registry data failed integrity validation.");
                return new TrialStatus
                {
                    IsExpired = true,
                    IsTrialActive = false,
                    InstallDate = installDate,
                    TrialEndDate = installDate.AddDays(TRIAL_DAYS),
                    RefreshesRemaining = 0,
                    TotalRefreshes = MAX_TRIAL_REFRESHES
                };
            }

            var refreshCount = GetRefreshCount();
            var trialEndDate = installDate.AddDays(TRIAL_DAYS);
            var now = DateTime.Now;
            var daysRemaining = Math.Max(0, (int)Math.Ceiling((trialEndDate - now).TotalDays));
            var refreshesRemaining = Math.Max(0, MAX_TRIAL_REFRESHES - refreshCount);
            var isExpired = now > trialEndDate || refreshCount >= MAX_TRIAL_REFRESHES;

            return new TrialStatus
            {
                IsTrialActive = !isExpired,
                DaysRemaining = daysRemaining,
                RefreshesRemaining = refreshesRemaining,
                IsExpired = isExpired,
                InstallDate = installDate,
                TrialEndDate = trialEndDate,
                TotalRefreshes = refreshCount,
                MinutesRemaining = 0,
                IsMinutesMode = false
            };
        }
        catch (Exception ex)
        {
            LogService.Error("TrialManager: Failed to get trial status.", ex);
            return new TrialStatus { IsExpired = true };
        }
    }

    /// <summary>
    /// Records a successful model check refresh.
    /// </summary>
    public static void RecordRefresh()
    {
        try
        {
            var currentCount = GetRefreshCount();
            SetRefreshCount(currentCount + 1);
            LogService.Info($"TrialManager: Refresh recorded. Total refreshes: {currentCount + 1}");
        }
        catch (Exception ex)
        {
            LogService.Error("TrialManager: Failed to record refresh.", ex);
        }
    }

    /// <summary>
    /// Checks whether the trial registry values match the stored integrity hash.
    /// </summary>
    public static bool IsTrialValid()
    {
        try
        {
            var storedHash = GetTrialHash();
            var currentHash = GenerateTrialHash();
            return storedHash == currentHash;
        }
        catch
        {
            return false;
        }
    }

    private static DateTime GetInstallDate()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            if (key is null)
                return DateTime.Now.AddDays(-TRIAL_DAYS);

            MigrateOldTrialIfNeeded(key);

            var dateString = key.GetValue(InstallDateKey) as string;
            if (DateTime.TryParse(dateString, out var installDate))
                return installDate;

            InitializeNewTrial(key);
            dateString = key.GetValue(InstallDateKey) as string;
            if (DateTime.TryParse(dateString, out installDate))
                return installDate;

            return DateTime.Now.AddDays(-TRIAL_DAYS);
        }
        catch (Exception ex)
        {
            LogService.Error("TrialManager: Failed to get or set install date.", ex);
            return DateTime.Now.AddDays(-TRIAL_DAYS);
        }
    }

    private static void InitializeNewTrial(RegistryKey key)
    {
        try
        {
            var now = DateTime.Now;
            key.SetValue(InstallDateKey, now.ToString("O"));
            key.SetValue(RefreshCountKey, 0);
            key.SetValue(TrialVersionKey, TRIAL_VERSION);
            SetTrialHash();
            LogService.Info($"TrialManager: Trial v{TRIAL_VERSION} started on {now:yyyy-MM-dd}.");
        }
        catch (Exception ex)
        {
            LogService.Error("TrialManager: Failed to initialize new trial.", ex);
        }
    }

    private static void MigrateOldTrialIfNeeded(RegistryKey key)
    {
        try
        {
            var versionObj = key.GetValue(TrialVersionKey, 0);
            var version = 0;
            if (versionObj is int i)
                version = i;
            else if (versionObj is not null)
                int.TryParse(versionObj.ToString(), out version);

            var hasData =
                key.GetValue(InstallDateKey) is not null ||
                key.GetValue(RefreshCountKey) is not null ||
                key.GetValue(TrialHashKey) is not null;

            if (!hasData || version >= TRIAL_VERSION)
                return;

            LogService.Info($"TrialManager: Existing trial data detected (version {version}), resetting for v{TRIAL_VERSION}.");
            key.DeleteValue(InstallDateKey, false);
            key.DeleteValue(RefreshCountKey, false);
            key.DeleteValue(TrialHashKey, false);

            InitializeNewTrial(key);
        }
        catch (Exception ex)
        {
            LogService.Error("TrialManager: Failed to migrate old trial data.", ex);
        }
    }

    private static int GetRefreshCount()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            var value = key?.GetValue(RefreshCountKey) ?? 0;
            if (value is int i)
                return i;

            return int.TryParse(value.ToString(), out var count) ? count : 0;
        }
        catch
        {
            return MAX_TRIAL_REFRESHES;
        }
    }

    private static void SetRefreshCount(int count)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key?.SetValue(RefreshCountKey, count);
            SetTrialHash();
        }
        catch (Exception ex)
        {
            LogService.Error("TrialManager: Failed to set refresh count.", ex);
        }
    }

    private static string GenerateTrialHash()
    {
        try
        {
            var installDate = GetInstallDate();
            var refreshCount = GetRefreshCount();
            var machineId = GetMachineId();
            var data = $"{installDate:O}|{refreshCount}|{machineId}|ModenaAEC_ModelHealthChecker_Trial";

            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hashBytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void SetTrialHash()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key?.SetValue(TrialHashKey, GenerateTrialHash());
        }
        catch (Exception ex)
        {
            LogService.Error("TrialManager: Failed to set trial hash.", ex);
        }
    }

    private static string GetTrialHash()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            return key?.GetValue(TrialHashKey) as string ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetMachineId()
    {
        try
        {
            var data = $"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion}";

            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hashBytes).Substring(0, 16);
        }
        catch
        {
            return "UNKNOWN";
        }
    }
}

/// <summary>
/// Represents the current trial status.
/// </summary>
public class TrialStatus
{
    public bool IsTrialActive { get; set; }
    public bool IsExpired { get; set; }
    public int DaysRemaining { get; set; }
    public int RefreshesRemaining { get; set; }
    public DateTime InstallDate { get; set; }
    public DateTime TrialEndDate { get; set; }
    public int TotalRefreshes { get; set; }
    public int MinutesRemaining { get; set; }
    public bool IsMinutesMode { get; set; }

    public string GetStatusMessage()
    {
        if (IsExpired)
            return "Trial period has expired. Please purchase a license to continue using Modena Model Health Checker.";

        if (DaysRemaining <= 3 || RefreshesRemaining <= 10)
            return $"Trial expires in {DaysRemaining} days or {RefreshesRemaining} refreshes. Please purchase a license to continue uninterrupted.";

        return $"Trial active: {DaysRemaining} days and {RefreshesRemaining} refreshes remaining.";
    }
}
#endif // TRIAL_VERSION
