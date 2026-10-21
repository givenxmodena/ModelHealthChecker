using Modena.RevitAddin.Services;
using Modena.Shared.DTOs;

namespace Modena.RevitAddin.ViewModels;

public sealed class HistoryViewModel : BaseViewModel
{
    private readonly HealthHistoryService _service;
    private string _modelKey = string.Empty;
    private int _loadVersion;
    private bool _isLoading;
    private string _status = string.Empty;
    private string _saveWarning = string.Empty;

    public HistoryViewModel(HealthHistoryService service)
    {
        _service = service;
        ReloadCommand = new AsyncRelayCommand(() => LoadAsync(_modelKey));
    }

    public IReadOnlyList<HealthScanRecord> Records { get; private set; } = Array.Empty<HealthScanRecord>();
    public bool HasHistory => Records.Count > 0;
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string SaveWarning { get => _saveWarning; set => SetProperty(ref _saveWarning, value); }
    public AsyncRelayCommand ReloadCommand { get; }

    public async Task LoadAsync(string modelKey)
    {
        if (string.IsNullOrWhiteSpace(modelKey)) return;
        _modelKey = modelKey;
        var version = ++_loadVersion;
        IsLoading = true;
        Status = "Loading scan history…";
        try
        {
            var records = await Task.Run(() => _service.LoadHistory(modelKey));
            if (version != _loadVersion) return;
            SetRecords(records);
            Status = records.Count == 0 ? "History starts with your next successful scan." : "";
        }
        catch (Exception ex)
        {
            if (version != _loadVersion) return;
            LogService.Warn($"History: Could not load scans. {ex.Message}");
            Status = "History could not be loaded. Use Reload history to try again.";
        }
        finally { if (version == _loadVersion) IsLoading = false; }
    }

    private void SetRecords(IReadOnlyList<HealthScanRecord> records)
    {
        Records = records;
        OnPropertyChanged(nameof(Records));
        OnPropertyChanged(nameof(HasHistory));
    }
}
