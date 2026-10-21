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
    private HistoryScanItem? _baseline;
    private HistoryScanItem? _current;
    private bool _updatingScans;
    private string _preset = "Previous";

    public HistoryViewModel(HealthHistoryService service)
    {
        _service = service;
        SelectBaselineCommand = new RelayCommand(p => Baseline = p as HistoryScanItem);
        SelectCurrentCommand = new RelayCommand(p => Current = p as HistoryScanItem);
        ReloadCommand = new AsyncRelayCommand(() => LoadAsync(_modelKey));
    }

    public IReadOnlyList<HealthScanRecord> Records { get; private set; } = Array.Empty<HealthScanRecord>();
    public IReadOnlyList<HistoryScanItem> Scans { get; private set; } = Array.Empty<HistoryScanItem>();
    public bool HasHistory => Records.Count > 0;
    public bool CanCompare => Records.Count > 1;
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string SaveWarning { get => _saveWarning; set => SetProperty(ref _saveWarning, value); }
    public HistoryScanItem? Baseline
    {
        get => _baseline;
        set { if (!_updatingScans && SetProperty(ref _baseline, value)) { _preset = "Manual"; UpdateSelection(); } }
    }
    public HistoryScanItem? Current
    {
        get => _current;
        set { if (!_updatingScans && SetProperty(ref _current, value)) { _preset = "Manual"; UpdateSelection(); } }
    }

    public RelayCommand SelectBaselineCommand { get; }
    public RelayCommand SelectCurrentCommand { get; }
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
        var baselineId = Baseline?.Record.Id;
        var currentId = Current?.Record.Id;
        Records = records;
        Scans = records.Select((r, i) => new HistoryScanItem(r,
            i == 0 ? null : (int?)(r.PassRate - records[i - 1].PassRate))).Reverse().ToList();
        _baseline = Scans.FirstOrDefault(s => s.Record.Id == baselineId);
        _current = Scans.FirstOrDefault(s => s.Record.Id == currentId);
        if (_preset != "Manual" || _baseline is null || _current is null) ApplyPreset();
        // ComboBox clears SelectedItem when its ItemsSource changes. Keep that UI
        // transition from replacing the restored selection or the active preset.
        _updatingScans = true;
        try
        {
            foreach (var name in new[] { nameof(Records), nameof(Scans), nameof(HasHistory), nameof(CanCompare),
                     nameof(Baseline), nameof(Current) })
                OnPropertyChanged(name);
        }
        finally { _updatingScans = false; }
        UpdateSelection();
    }

    private void ApplyPreset()
    {
        _current = Scans.FirstOrDefault();
        _baseline = !CanCompare ? null : Scans[1];
    }

    private void UpdateSelection()
    {
        foreach (var scan in Scans)
        {
            scan.IsBaseline = ReferenceEquals(scan, Baseline);
            scan.IsCurrent = ReferenceEquals(scan, Current);
        }
    }

    public static string FormatDelta(int delta) => delta == 0 ? "No change" : $"{delta:+0;-0} pp";
}

public sealed class HistoryScanItem : BaseViewModel
{
    private bool _isBaseline;
    private bool _isCurrent;
    public HistoryScanItem(HealthScanRecord record, int? delta) { Record = record; Delta = delta; }
    public HealthScanRecord Record { get; }
    public int? Delta { get; }
    public string Date => Record.Timestamp.ToLocalTime().ToString("d MMM yyyy");
    public string Time => Record.Timestamp.ToLocalTime().ToString("HH:mm:ss");
    public string Label => Record.Timestamp.ToLocalTime().ToString("d MMM yyyy · HH:mm:ss") + $" · {Record.PassRate}%";
    public string PreviousDelta => Delta.HasValue ? HistoryViewModel.FormatDelta(Delta.Value) : "—";
    public string Passed => $"{Record.PassedChecks} / {Record.TotalChecks}";
    public bool IsBaseline { get => _isBaseline; set => SetProperty(ref _isBaseline, value); }
    public bool IsCurrent { get => _isCurrent; set => SetProperty(ref _isCurrent, value); }
}

