using System.Collections.ObjectModel;
using System.Windows.Threading;
using Modena.RevitAddin.RevitApi;
using Modena.RevitAddin.Services;
using Modena.Shared.DTOs;

namespace Modena.RevitAddin.ViewModels;

/// <summary>
/// ViewModel for the Model Health Checker dashboard.
/// Loading is split into two phases:
///   Phase 1 (fast)  — categories, warnings, health checks. Renders within a few seconds.
///   Phase 2 (slow)  — family sizes (opens each family document). Runs after fast data is visible.
/// A Task.Yield() between the phases lets WPF render the fast results before family
/// extraction starts blocking the dispatcher thread.
/// </summary>
public class ModelHealthViewModel : BaseViewModel
{
    private readonly IModelHealthExtractor _extractor;
    private readonly IRevitDocumentContext _documentContext;
    private readonly PluginConfig _config;
    private readonly RefreshTimerService _timer;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _cts;
    private bool _timerStarted;
    private bool _isSilentRefreshing;
    private DateTime? _familiesCachedAt;
    private bool _isSizeExtracting;
    private bool _hasSizeData;
    private string _sizeProgressText = string.Empty;

    // Backing fields
    private string _modelName = string.Empty;
    private string _projectName = string.Empty;
    private string _lastRefreshedText = string.Empty;
    private bool _isLoading;
    private bool _isFamiliesLoading;
    private bool _isBackgroundRefreshing;
    private bool _hasData;
    private string? _errorMessage;
    private string _statusText = "Ready";
    private SummaryDto _summary = new();

    public ModelIdentity ModelIdentity { get; }

    /// <summary>True when the loaded config has validation warnings to surface in the UI.</summary>
    public bool   HasConfigWarning  { get; }
    /// <summary>Human-readable summary of the config warning(s).</summary>
    public string ConfigWarningText { get; }

    public string ModelName
    {
        get => _modelName;
        private set => SetProperty(ref _modelName, value);
    }

    public string ProjectName
    {
        get => _projectName;
        private set => SetProperty(ref _projectName, value);
    }

    public string LastRefreshedText
    {
        get => _lastRefreshedText;
        private set => SetProperty(ref _lastRefreshedText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
                OnPropertyChanged(nameof(IsInitialLoading));
        }
    }

    /// <summary>
    /// True only while the very first load is in progress (before any data has appeared).
    /// Used to drive the full-screen loading spinner — it hides once fast data is ready.
    /// </summary>
    public bool IsInitialLoading => _isLoading && !_hasData;

    /// <summary>True while family sizes are being extracted in the background.</summary>
    public bool IsFamiliesLoading
    {
        get => _isFamiliesLoading;
        private set => SetProperty(ref _isFamiliesLoading, value);
    }

    /// <summary>True while the auto-refresh timer is running a silent background sync.</summary>
    public bool IsBackgroundRefreshing
    {
        get => _isBackgroundRefreshing;
        private set => SetProperty(ref _isBackgroundRefreshing, value);
    }

    public bool HasData
    {
        get => _hasData;
        private set
        {
            if (SetProperty(ref _hasData, value))
                OnPropertyChanged(nameof(IsInitialLoading));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public SummaryDto Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public ObservableCollection<FailedCheckDto> FailedChecks   { get; } = new();
    public ObservableCollection<MetricDto>      Metrics        { get; } = new();
    public ObservableCollection<CategoryDto>    Categories     { get; } = new();
    public ObservableCollection<FamilyDto>      Families       { get; } = new();
    public ObservableCollection<FamilyDto>      FamiliesBySize { get; } = new();
    public ObservableCollection<string>         PassedChecks   { get; } = new();

    /// <summary>True while the on-demand KB extraction is running.</summary>
    public bool IsSizeExtracting
    {
        get => _isSizeExtracting;
        private set
        {
            if (SetProperty(ref _isSizeExtracting, value))
            {
                OnPropertyChanged(nameof(ShowSizePrompt));
                OnPropertyChanged(nameof(ShowSizeResults));
            }
        }
    }

    /// <summary>True once KB extraction has completed at least once.</summary>
    public bool HasSizeData
    {
        get => _hasSizeData;
        private set
        {
            if (SetProperty(ref _hasSizeData, value))
            {
                OnPropertyChanged(nameof(ShowSizePrompt));
                OnPropertyChanged(nameof(ShowSizeResults));
            }
        }
    }

    /// <summary>Shows the "Extract Sizes" prompt — before any extraction has run.</summary>
    public bool ShowSizePrompt  => !_isSizeExtracting && !_hasSizeData;

    /// <summary>Shows the results list — extraction done and not currently re-running.</summary>
    public bool ShowSizeResults => !_isSizeExtracting && _hasSizeData;

    /// <summary>Running status text shown while KB extraction is in progress.</summary>
    public string SizeProgressText
    {
        get => _sizeProgressText;
        private set => SetProperty(ref _sizeProgressText, value);
    }

    public AsyncRelayCommand LoadCommand           { get; }
    public AsyncRelayCommand RefreshCommand        { get; }
    public AsyncRelayCommand LoadFamilySizesCommand { get; }

    public ModelHealthViewModel(
        ModelIdentity modelIdentity,
        IModelHealthExtractor extractor,
        IRevitDocumentContext documentContext,
        PluginConfig config,
        Dispatcher? dispatcher = null,
        RefreshTimerService? timer = null)
    {
        ModelIdentity    = modelIdentity   ?? throw new ArgumentNullException(nameof(modelIdentity));
        _extractor       = extractor       ?? throw new ArgumentNullException(nameof(extractor));
        _documentContext = documentContext ?? throw new ArgumentNullException(nameof(documentContext));
        _config          = config          ?? throw new ArgumentNullException(nameof(config));
        _dispatcher      = dispatcher      ?? Dispatcher.CurrentDispatcher;
        _timer           = timer           ?? new RefreshTimerService();

        // Pre-populate from the document context so the header shows the model name
        // the moment the window opens, before any extraction runs (satisfies MHC-7 AC1).
        ModelName   = documentContext.DocumentTitle ?? string.Empty;
        ProjectName = ExtractProjectNameFromContext(documentContext);

        var warnings = _config.ValidationWarnings;
        HasConfigWarning = warnings.Count > 0;
        ConfigWarningText = warnings.Count switch
        {
            0 => string.Empty,
            1 => $"Configuration warning: {warnings[0]}",
            _ => $"Configuration has {warnings.Count} issues — check app logs for details."
        };

        LoadCommand            = new AsyncRelayCommand(ExecuteLoadAsync);
        RefreshCommand         = new AsyncRelayCommand(ExecuteRefreshAsync);
        LoadFamilySizesCommand = new AsyncRelayCommand(ExecuteLoadFamilySizesAsync);
    }

    public bool IsTimerRunning => _timer.IsRunning;

    private async Task ExecuteLoadAsync()    => await FetchDataAsync(isRefresh: false, isSilent: false);
    private async Task ExecuteRefreshAsync() => await FetchDataAsync(isRefresh: true,  isSilent: false);

    private async Task FetchDataAsync(bool isRefresh, bool isSilent)
    {
        // Interactive loads block each other. Silent refreshes skip if one is already running,
        // but an interactive load always takes priority and cancels any in-progress silent refresh.
        if (IsLoading) return;
        if (isSilent && _isSilentRefreshing) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        if (isSilent)
        {
            _isSilentRefreshing    = true;
            IsBackgroundRefreshing = true;
        }
        else
        {
            IsLoading         = true;
            IsFamiliesLoading = false;
            ErrorMessage      = null;
            StatusText        = isRefresh ? "Refreshing model data..." : "Analysing model...";
        }

        LogService.Info($"ViewModel: {(isSilent ? "Silent refresh" : isRefresh ? "Refresh" : "Load")} started.");

        // Snapshot families so they can be restored if an interactive refresh fails or is cancelled.
        var familiesSnapshot = !isSilent && isRefresh && HasData ? Families.ToList() : null;

        try
        {
            if (isSilent)
            {
                // ── Silent path: run both phases, then apply atomically so the user
                //    never sees a flash of empty state between Phase 1 and Phase 2. ─────────
                var fastResponse = await _extractor.ExtractFastAsync(_documentContext, ct);
                ct.ThrowIfCancellationRequested();

                // Evaluate cache BEFORE ApplyFastResponse clears the Families collection.
                var cacheAge      = _familiesCachedAt.HasValue ? DateTime.UtcNow - _familiesCachedAt.Value : TimeSpan.MaxValue;
                var silentCacheOk = HasData && Families.Count > 0 && cacheAge.TotalMinutes < _config.FamilyCacheMinutes;

                List<FamilyDto> families;
                if (silentCacheOk)
                {
                    LogService.Info($"ViewModel: Silent refresh — family cache hit (age={cacheAge.TotalMinutes:F1} min).");
                    families = Families.ToList();
                }
                else
                {
                    await Task.Yield();
                    ct.ThrowIfCancellationRequested();
                    families = await _extractor.ExtractFamilySizesAsync(_documentContext, ct);
                    ct.ThrowIfCancellationRequested();
                    _familiesCachedAt = DateTime.UtcNow;
                }

                ApplyFastResponse(fastResponse);
                ReplaceCollection(Families, families);
                UpdateFamilyMetric(families.Count);
                HasData           = true;
                LastRefreshedText = $"Last updated {DateTime.Now:HH:mm}";
                StatusText        = LastRefreshedText;
                LogService.Info("ViewModel: Silent refresh complete.");
            }
            else
            {
                // ── Interactive path: two-phase with loading indicators ───────────────────
                var fastResponse = await _extractor.ExtractFastAsync(_documentContext, ct);
                ct.ThrowIfCancellationRequested();

                ApplyFastResponse(fastResponse);
                HasData           = true;
                IsFamiliesLoading = true;
                StatusText        = "Loading family data...";

                await Task.Yield();
                ct.ThrowIfCancellationRequested();

                // cacheValid uses the snapshot count because ApplyFastResponse already cleared Families.
                var cacheAge   = _familiesCachedAt.HasValue ? DateTime.UtcNow - _familiesCachedAt.Value : TimeSpan.MaxValue;
                var cacheValid = isRefresh && (familiesSnapshot?.Count ?? 0) > 0 && cacheAge.TotalMinutes < _config.FamilyCacheMinutes;

                if (cacheValid)
                {
                    LogService.Info($"ViewModel: Family cache hit (age={cacheAge.TotalMinutes:F1} min). Skipping re-extraction.");
                    ReplaceCollection(Families, familiesSnapshot!);
                    UpdateFamilyMetric(Families.Count);
                }
                else
                {
                    var families = await _extractor.ExtractFamilySizesAsync(_documentContext, ct);
                    ct.ThrowIfCancellationRequested();
                    ReplaceCollection(Families, families);
                    UpdateFamilyMetric(families?.Count ?? 0);
                    _familiesCachedAt = DateTime.UtcNow;
                }

                LastRefreshedText = $"Last updated {DateTime.Now:HH:mm}";
                StatusText        = LastRefreshedText;
                LogService.Info("ViewModel: Data loaded successfully.");

                if (!_timerStarted && _config.AutoRefreshEnabled)
                {
                    var interval = TimeSpan.FromMinutes(_config.RefreshIntervalMinutes);
                    _timer.Start(interval, () => FetchDataAsync(isRefresh: true, isSilent: true));
                    _timerStarted = true;
                    LogService.Info("ViewModel: Auto-refresh timer started.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (familiesSnapshot is not null)
                ReplaceCollection(Families, familiesSnapshot);
            LogService.Info($"ViewModel: {(isSilent ? "Silent refresh" : "Operation")} was cancelled.");
        }
        catch (Exception ex)
        {
            if (isSilent)
            {
                // Silent refresh failed — leave the dashboard exactly as it was.
                LogService.Error("ViewModel: Silent refresh failed; data unchanged.", ex);
            }
            else if (!HasData)
            {
                ErrorMessage = MapErrorMessage(ex);
                StatusText   = "Unable to load model health data";
                LogService.Error("ViewModel: Extraction failed.", ex);
            }
            else
            {
                if (familiesSnapshot is not null)
                    ReplaceCollection(Families, familiesSnapshot);
                var since  = string.IsNullOrEmpty(LastRefreshedText) ? "a previous session" : LastRefreshedText.ToLowerInvariant();
                StatusText = $"Refresh failed — showing data from {since}";
                LogService.Error("ViewModel: Refresh failed; preserving existing data.", ex);
            }
        }
        finally
        {
            if (isSilent)
            {
                _isSilentRefreshing    = false;
                IsBackgroundRefreshing = false;
            }
            else
            {
                IsLoading         = false;
                IsFamiliesLoading = false;
            }
        }
    }

    /// <summary>Applies fast-phase results (no families). Clears the Families collection.</summary>
    private void ApplyFastResponse(DashboardResponse response)
    {
        ModelName   = response.ModelName;
        ProjectName = response.ProjectName;
        Summary     = response.Summary ?? new SummaryDto();
        ReplaceCollection(FailedChecks, response.FailedChecks);
        ReplaceCollection(Metrics,      response.Metrics);
        ReplaceCollection(Categories,   response.Categories);
        ReplaceCollection(PassedChecks, response.PassedChecks);
        Families.Clear();
    }

    /// <summary>Applies a full response (fast data + families).</summary>
    private void ApplyResponse(DashboardResponse response)
    {
        ApplyFastResponse(response);
        ReplaceCollection(Families, response.Families);
        UpdateFamilyMetric(response.Families?.Count ?? 0);
    }

    /// <summary>Rebuilds the Metrics collection with the accurate family count.</summary>
    private void UpdateFamilyMetric(int familyCount)
    {
        var elementCount = Metrics.FirstOrDefault(m => m.Name == "Total Elements")?.Count ?? Summary.ModelElements;
        var warningCount = Metrics.FirstOrDefault(m => m.Name == "Warnings")?.Count       ?? Summary.Warnings;
        ReplaceCollection(Metrics, new List<MetricDto>
        {
            new() { Name = "Total Elements", Count = elementCount },
            new() { Name = "Warnings",       Count = warningCount },
            new() { Name = "Families",       Count = familyCount  }
        });
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, List<T>? source)
    {
        target.Clear();
        if (source is null) return;
        foreach (var item in source)
            target.Add(item);
    }

    private static string MapErrorMessage(Exception ex)
    {
        if (ex is TimeoutException || ex.Message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0)
            return "The extraction timed out. Please try again.";

        if (ex is InvalidOperationException)
            return "No health data found for this model.";

        return "Model health extraction failed. Please try again.";
    }

    private static string ExtractProjectNameFromContext(IRevitDocumentContext context)
    {
        if (!string.IsNullOrEmpty(context.ModelPath))
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(context.ModelPath);
                if (!string.IsNullOrEmpty(dir))
                    return System.IO.Path.GetFileName(dir);
            }
            catch { }
        }
        return string.Empty;
    }

    private async Task ExecuteLoadFamilySizesAsync()
    {
        if (_isSizeExtracting) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsSizeExtracting = true;
        SizeProgressText = "Starting extraction...";
        LogService.Info("ViewModel: On-demand family KB extraction started.");

        try
        {
            var families = await _extractor.ExtractFamilySizesKbAsync(
                _documentContext,
                (current, total) => SizeProgressText = $"Extracting... {current} of {total}",
                ct);

            ct.ThrowIfCancellationRequested();
            ReplaceCollection(FamiliesBySize, families);
            HasSizeData      = true;
            SizeProgressText = $"Done — {families.Count} families measured";
            LogService.Info($"ViewModel: Family KB extraction complete. Count={families.Count}");
        }
        catch (OperationCanceledException)
        {
            SizeProgressText = "Extraction cancelled.";
            LogService.Info("ViewModel: Family KB extraction cancelled.");
        }
        catch (Exception ex)
        {
            SizeProgressText = "Extraction failed — check app logs.";
            LogService.Error("ViewModel: Family KB extraction failed.", ex);
        }
        finally
        {
            IsSizeExtracting = false;
        }
    }

    /// <summary>Stops the timer and releases resources. Called when the window closes.</summary>
    public void Cleanup()
    {
        LogService.Info("ViewModel: Cleanup called.");
        _timer.Stop();
        _timer.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts          = null;
        _timerStarted = false;
    }
}
