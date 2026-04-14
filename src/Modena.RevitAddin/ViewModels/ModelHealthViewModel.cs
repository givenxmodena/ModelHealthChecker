using System.Collections.ObjectModel;
using System.Windows.Threading;
using Modena.RevitAddin.RevitApi;
using Modena.RevitAddin.Services;
using Modena.Shared.DTOs;

namespace Modena.RevitAddin.ViewModels;

/// <summary>
/// ViewModel for the Model Health Checker dashboard.
/// Manages load/refresh commands, auto-refresh timer, and all bound UI state.
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

    // Backing fields
    private string _modelName = string.Empty;
    private string _projectName = string.Empty;
    private string _lastRefreshedText = string.Empty;
    private bool _isLoading;
    private bool _hasData;
    private string? _errorMessage;
    private string _statusText = "Ready";
    private SummaryDto _summary = new();

    public ModelIdentity ModelIdentity { get; }

    // --- Scalar properties ---
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
        private set => SetProperty(ref _isLoading, value);
    }

    public bool HasData
    {
        get => _hasData;
        private set => SetProperty(ref _hasData, value);
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

    // --- Collections ---
    public ObservableCollection<FailedCheckDto> FailedChecks { get; } = new();
    public ObservableCollection<MetricDto> Metrics { get; } = new();
    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<FamilyDto> Families { get; } = new();
    public ObservableCollection<string> PassedChecks { get; } = new();

    // --- Commands ---
    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>
    /// Creates a new ModelHealthViewModel.
    /// </summary>
    /// <param name="modelIdentity">Identity of the currently open Revit model.</param>
    /// <param name="extractor">Extractor for reading health data from the Revit document.</param>
    /// <param name="documentContext">Revit document context for extraction.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <param name="dispatcher">Optional dispatcher for unit-testing; defaults to current.</param>
    /// <param name="timer">Optional timer for unit-testing; defaults to new instance.</param>
    public ModelHealthViewModel(
        ModelIdentity modelIdentity,
        IModelHealthExtractor extractor,
        IRevitDocumentContext documentContext,
        PluginConfig config,
        Dispatcher? dispatcher = null,
        RefreshTimerService? timer = null)
    {
        ModelIdentity = modelIdentity ?? throw new ArgumentNullException(nameof(modelIdentity));
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _documentContext = documentContext ?? throw new ArgumentNullException(nameof(documentContext));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        _timer = timer ?? new RefreshTimerService();

        LoadCommand = new AsyncRelayCommand(ExecuteLoadAsync);
        RefreshCommand = new AsyncRelayCommand(ExecuteRefreshAsync);
    }

    /// <summary>
    /// Exposes whether the auto-refresh timer is currently running.
    /// </summary>
    public bool IsTimerRunning => _timer.IsRunning;

    private async Task ExecuteLoadAsync()
    {
        await FetchDataAsync(isRefresh: false);
    }

    private async Task ExecuteRefreshAsync()
    {
        await FetchDataAsync(isRefresh: true);
    }

    private async Task FetchDataAsync(bool isRefresh)
    {
        // Reentrancy guard
        if (IsLoading) return;

        IsLoading = true;
        ErrorMessage = null;
        StatusText = "Loading latest model health data...";
        LogService.Info($"ViewModel: {(isRefresh ? "Refresh" : "Load")} started.");

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            var response = await _extractor.ExtractAsync(_documentContext, _cts.Token);

            if (response is not null)
            {
                ApplyResponse(response);
                HasData = true;
                ErrorMessage = null;
                LastRefreshedText = $"Last updated {DateTime.Now:HH:mm}";
                StatusText = LastRefreshedText;
                LogService.Info("ViewModel: Data loaded successfully.");

                // Start timer after first successful load
                if (!_timerStarted && _config.AutoRefreshEnabled)
                {
                    var interval = TimeSpan.FromMinutes(_config.RefreshIntervalMinutes);
                    _timer.Start(interval, () => FetchDataAsync(isRefresh: true));
                    _timerStarted = true;
                    LogService.Info("ViewModel: Auto-refresh timer started.");
                }
            }
            else
            {
                ErrorMessage = "No health data found for this model.";
                StatusText = "Unable to load model health data";
                LogService.Warn("ViewModel: Extraction returned null.");
            }
        }
        catch (OperationCanceledException)
        {
            LogService.Info("ViewModel: Operation was cancelled.");
        }
        catch (Exception ex)
        {
            ErrorMessage = MapErrorMessage(ex);
            StatusText = "Unable to load model health data";
            LogService.Error("ViewModel: Extraction failed.", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyResponse(DashboardResponse response)
    {
        ModelName = response.ModelName;
        ProjectName = response.ProjectName;
        Summary = response.Summary ?? new SummaryDto();

        ReplaceCollection(FailedChecks, response.FailedChecks);
        ReplaceCollection(Metrics, response.Metrics);
        ReplaceCollection(Categories, response.Categories);
        ReplaceCollection(Families, response.Families);
        ReplaceCollection(PassedChecks, response.PassedChecks);
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

    /// <summary>
    /// Stops the timer and releases resources. Called when the window closes.
    /// </summary>
    public void Cleanup()
    {
        LogService.Info("ViewModel: Cleanup called.");
        _timer.Stop();
        _timer.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _timerStarted = false;
    }
}
