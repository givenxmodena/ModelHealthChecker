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
    private HealthScanRecord? _best;
    private CheckChangeFilter _selectedChangeFilter;
    private bool _updatingScans;
    private string _preset = "Previous";
    private IReadOnlyList<HealthCheckDelta> _allChanges = Array.Empty<HealthCheckDelta>();
    private int _pageIndex;
    public const int PageSize = 10;

    public HistoryViewModel(HealthHistoryService service)
    {
        _service = service;
        SelectBaselineCommand = new RelayCommand(p => Baseline = p as HistoryScanItem);
        SelectCurrentCommand = new RelayCommand(p => Current = p as HistoryScanItem);
        ComparePreviousCommand = new RelayCommand(() => SelectPreset("Previous"));
        CompareBestCommand = new RelayCommand(() => SelectPreset("Best"));
        ReloadCommand = new AsyncRelayCommand(() => LoadAsync(_modelKey));
        PreviousPageCommand = new RelayCommand(() => ChangePage(-1), () => _pageIndex > 0);
        NextPageCommand = new RelayCommand(() => ChangePage(1), () => _pageIndex + 1 < PageCount);
    }

    public IReadOnlyList<HealthScanRecord> Records { get; private set; } = Array.Empty<HealthScanRecord>();
    public IReadOnlyList<HistoryScanItem> Scans { get; private set; } = Array.Empty<HistoryScanItem>();
    public IReadOnlyList<HistoryScanItem> PageScans { get; private set; } = Array.Empty<HistoryScanItem>();
    public int PageCount => (Scans.Count + PageSize - 1) / PageSize;
    public int PageNumber => HasHistory ? _pageIndex + 1 : 0;
    public string PageSummary => $"Page {PageNumber:N0} of {PageCount:N0}";
    public string PageRange => HasHistory
        ? $"{_pageIndex * PageSize + 1:N0}–{Math.Min((_pageIndex + 1) * PageSize, Scans.Count):N0} of {Scans.Count:N0} scans"
        : "No scans recorded";
    public IReadOnlyList<CheckChangeFilter> ChangeFilters { get; } = new[]
        { CheckChangeFilter.Changed, CheckChangeFilter.Worsened, CheckChangeFilter.Improved, CheckChangeFilter.All };
    public IReadOnlyList<HealthCheckDelta> Changes => _allChanges.Where(c => SelectedChangeFilter switch
    {
        CheckChangeFilter.Worsened => c.IsWorsened,
        CheckChangeFilter.Improved => c.IsImproved,
        CheckChangeFilter.All => true,
        _ => c.Status != "Unchanged"
    }).ToList();
    public bool HasHistory => Records.Count > 0;
    public bool CanCompare => Records.Count > 1;
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string SaveWarning { get => _saveWarning; set => SetProperty(ref _saveWarning, value); }
    public string ScanCount => Records.Count.ToString("N0");
    public string Since => HasHistory ? "since " + Records[0].Timestamp.ToLocalTime().ToString("d MMM yyyy") : "No scans yet";
    public string LatestPassRate => HasHistory ? Records[Records.Count - 1].PassRate + "%" : "—";
    public int? LatestChange => Records.Count > 1 ? Records[Records.Count - 1].PassRate - Records[Records.Count - 2].PassRate : (int?)null;
    public int LatestDirection => LatestChange.HasValue ? Math.Sign(LatestChange.Value) : 0;
    public string LatestDelta => LatestChange.HasValue
        ? (LatestChange > 0 ? "▲ " : LatestChange < 0 ? "▼ " : "") + FormatDelta(LatestChange.Value) : "—";
    public string LatestComparisonCaption => CanCompare
        ? $"vs previous scan ({Records[Records.Count - 2].Timestamp.ToLocalTime():d MMM})"
        : HasHistory ? "First scan" : "No scans yet";
    private HealthScanRecord? Best => _best;
    public string BestPassRate => Best is { } best ? best.PassRate + "%" : "—";
    public string BestDate => Best?.Timestamp.ToLocalTime().ToString("d MMM yyyy") ?? "No scans yet";
    public int? ChangeSinceBestValue => HasHistory ? Records[Records.Count - 1].PassRate - Best!.PassRate : (int?)null;
    public string ChangeSinceBest => ChangeSinceBestValue.HasValue ? $"{ChangeSinceBestValue.Value:+0;-0;0} pp" : "—";
    public bool IsBelowBest => ChangeSinceBestValue < 0;
    public string BestComparisonCaption => Best is null ? "No scans yet"
        : $"{(IsBelowBest ? "below" : "at")} {Best.Timestamp.ToLocalTime():d MMM} best";
    public int? ComparisonChange => Baseline is null || Current is null ? (int?)null : Current.Record.PassRate - Baseline.Record.PassRate;
    public string ComparisonDelta => Baseline is null || Current is null ? "Select two scans" : FormatDelta(Current.Record.PassRate - Baseline.Record.PassRate);
    public string ComparisonRate => Baseline is null || Current is null ? "—" : $"{Baseline.Record.PassRate}% → {Current.Record.PassRate}%";
    public string CoverageSummary => !CanCompare ? "Two scans are needed to compare results."
        : string.Join(" · ", _allChanges.GroupBy(c => c.Status).Select(g => $"{g.Count()} {g.Key.ToLowerInvariant()}"));
    public string BaselineCaption => Baseline is null ? "Baseline: select a scan" : $"Baseline: {Baseline.Record.Timestamp.ToLocalTime():d MMM · HH:mm:ss}";
    public string CurrentCaption => Current is null ? "Current: select a scan" : $"Current: {Current.Record.Timestamp.ToLocalTime():d MMM · HH:mm:ss}";
    public string ComparisonSummary
    {
        get
        {
            if (Baseline is null || Current is null) return "Select a baseline and current scan to compare.";
            if (_allChanges.Count == 0) return "No evaluated checks were recorded for these scans.";
            var phrases = new[]
            {
                Describe("Newly failing", "check started failing", "checks started failing"),
                Describe("Regressed", "existing issue worsened", "existing issues worsened"),
                Describe("Resolved", "check now passes", "checks now pass"),
                Describe("Improved", "existing issue improved", "existing issues improved"),
                Describe("Not comparable", "check cannot be compared", "checks cannot be compared")
            }.Where(p => p.Length > 0).ToList();
            return phrases.Count > 0 ? string.Join("; ", phrases) + "." : "No check results changed.";
        }
    }
    public string EmptyChangesMessage => !CanCompare || Baseline is null || Current is null
        ? "Two selected scans are needed to compare results."
        : _allChanges.Count == 0 ? "No evaluated checks were recorded for these scans."
        : SelectedChangeFilter switch
        {
            CheckChangeFilter.Worsened => "No checks worsened between these scans.",
            CheckChangeFilter.Improved => "No checks improved between these scans.",
            CheckChangeFilter.All => "No evaluated checks were recorded for these scans.",
            _ => "No check results changed between these scans."
        };
    public string ComparisonNote => !CanCompare ? "" : _allChanges.Any(c => c.Status == "Not comparable")
        ? "Check sets differ. Missing checks are not treated as passing. Counts show affected items."
        : "Counts show affected items. pp means percentage points.";

    public CheckChangeFilter SelectedChangeFilter
    {
        get => _selectedChangeFilter;
        set
        {
            if (!SetProperty(ref _selectedChangeFilter, value)) return;
            OnPropertyChanged(nameof(Changes));
            OnPropertyChanged(nameof(EmptyChangesMessage));
        }
    }

    private string Describe(string status, string singular, string plural)
    {
        var count = _allChanges.Count(c => c.Status == status);
        return count == 0 ? "" : $"{count} {(count == 1 ? singular : plural)}";
    }

    public HistoryScanItem? Baseline
    {
        get => _baseline;
        set { if (!_updatingScans && SetProperty(ref _baseline, value)) { _preset = "Manual"; UpdateComparison(); } }
    }
    public HistoryScanItem? Current
    {
        get => _current;
        set { if (!_updatingScans && SetProperty(ref _current, value)) { _preset = "Manual"; UpdateComparison(); } }
    }

    public RelayCommand SelectBaselineCommand { get; }
    public RelayCommand SelectCurrentCommand { get; }
    public RelayCommand ComparePreviousCommand { get; }
    public RelayCommand CompareBestCommand { get; }
    public AsyncRelayCommand ReloadCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }

    private void ChangePage(int offset)
    {
        var index = _pageIndex + offset;
        if (index < 0 || index >= PageCount) return;
        _pageIndex = index;
        UpdatePage();
    }

    private void UpdatePage()
    {
        _pageIndex = Math.Max(0, Math.Min(_pageIndex, PageCount - 1));
        PageScans = Scans.Skip(_pageIndex * PageSize).Take(PageSize).ToList();
        foreach (var name in new[] { nameof(PageScans), nameof(PageCount), nameof(PageNumber), nameof(PageSummary), nameof(PageRange) })
            OnPropertyChanged(name);
        PreviousPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }

    public async Task LoadAsync(string modelKey)
    {
        if (string.IsNullOrWhiteSpace(modelKey)) return;
        if (_modelKey != modelKey) _pageIndex = 0;
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
        var pageAnchor = _pageIndex > 0 ? PageScans.FirstOrDefault()?.Record.Id : null;
        Records = records;
        _best = null;
        foreach (var record in records)
            if (_best is null || record.PassRate >= _best.PassRate) _best = record;
        Scans = records.Select((r, i) => new HistoryScanItem(r,
            i == 0 ? null : (int?)(r.PassRate - records[i - 1].PassRate))).Reverse().ToList();
        if (pageAnchor is not null)
            for (var i = 0; i < Scans.Count; i++)
                if (Scans[i].Record.Id == pageAnchor) { _pageIndex = i / PageSize; break; }
        _baseline = Scans.FirstOrDefault(s => s.Record.Id == baselineId);
        _current = Scans.FirstOrDefault(s => s.Record.Id == currentId);
        if (_preset != "Manual" || _baseline is null || _current is null) ApplyPreset();
        // ComboBox clears SelectedItem when its ItemsSource changes. Keep that UI
        // transition from replacing the restored selection or the active preset.
        _updatingScans = true;
        try
        {
            foreach (var name in new[] { nameof(Records), nameof(Scans), nameof(HasHistory), nameof(CanCompare),
                     nameof(ScanCount), nameof(Since), nameof(LatestPassRate), nameof(LatestDelta), nameof(LatestChange),
                     nameof(LatestDirection), nameof(LatestComparisonCaption), nameof(BestComparisonCaption), nameof(IsBelowBest),
                     nameof(BestPassRate), nameof(BestDate), nameof(ChangeSinceBest), nameof(ChangeSinceBestValue), nameof(Baseline), nameof(Current) })
                OnPropertyChanged(name);
            UpdatePage();
        }
        finally { _updatingScans = false; }
        UpdateComparison();
    }

    private void SelectPreset(string preset)
    {
        _preset = preset;
        ApplyPreset();
        OnPropertyChanged(nameof(Baseline));
        OnPropertyChanged(nameof(Current));
        UpdateComparison();
    }

    private void ApplyPreset()
    {
        _current = Scans.FirstOrDefault();
        _baseline = !CanCompare ? null : _preset == "Best"
            ? Scans.OrderByDescending(s => s.Record.PassRate).First() : Scans[1];
    }

    private void UpdateComparison()
    {
        foreach (var scan in Scans)
        {
            scan.IsBaseline = ReferenceEquals(scan, Baseline);
            scan.IsCurrent = ReferenceEquals(scan, Current);
        }
        _allChanges = Baseline is null || Current is null ? Array.Empty<HealthCheckDelta>()
            : Compare(Baseline.Record, Current.Record);
        foreach (var name in new[] { nameof(Changes), nameof(ComparisonDelta), nameof(ComparisonRate), nameof(ComparisonChange),
                     nameof(CoverageSummary), nameof(ComparisonSummary), nameof(EmptyChangesMessage),
                     nameof(BaselineCaption), nameof(CurrentCaption), nameof(ComparisonNote) })
            OnPropertyChanged(name);
    }

    public static IReadOnlyList<HealthCheckDelta> Compare(HealthScanRecord baseline, HealthScanRecord current)
    {
        var before = EvaluatedChecks(baseline);
        var after = EvaluatedChecks(current);
        return before.Keys.Union(after.Keys, StringComparer.Ordinal).Select(name =>
        {
            before.TryGetValue(name, out var b);
            after.TryGetValue(name, out var c);
            return new HealthCheckDelta(name, b, c);
        }).OrderBy(c => c.SortOrder)
          .ThenBy(c => c.Name, StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, HealthCheckSummary> EvaluatedChecks(HealthScanRecord record)
    {
        var result = record.PassedCheckNames.Distinct(StringComparer.Ordinal)
            .ToDictionary(name => name, name => new HealthCheckSummary { Name = name }, StringComparer.Ordinal);
        foreach (var check in record.FailedCheckSummaries) result[check.Name] = check;
        return result;
    }

    public static string FormatDelta(int delta) => delta == 0 ? "No change" : $"{delta:+0;-0} pp";
}

public enum CheckChangeFilter { Changed, Worsened, Improved, All }

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

public sealed class HealthCheckDelta
{
    public HealthCheckDelta(string name, HealthCheckSummary? baseline, HealthCheckSummary? current)
    {
        Name = name;
        Category = (!string.IsNullOrEmpty(current?.Category) ? current : baseline)?.Category ?? "";
        Priority = (!string.IsNullOrEmpty(current?.Priority) ? current : baseline)?.Priority ?? "";
        BaselineCount = baseline?.Count;
        CurrentCount = current?.Count;
    }
    public string Name { get; }
    public string Category { get; }
    public string Priority { get; }
    public int? BaselineCount { get; }
    public int? CurrentCount { get; }
    public string Counts => $"{(BaselineCount.HasValue ? BaselineCount.ToString() : "—")} → {(CurrentCount.HasValue ? CurrentCount.ToString() : "—")}";
    public bool IsWorsened => Status == "Newly failing" || Status == "Regressed";
    public bool IsImproved => Status == "Resolved" || Status == "Improved";
    internal int SortOrder => Status switch
    {
        "Newly failing" => 0, "Regressed" => 1, "Resolved" => 2,
        "Improved" => 3, "Not comparable" => 4, _ => 5
    };
    public string Status => !BaselineCount.HasValue || !CurrentCount.HasValue ? "Not comparable"
        : BaselineCount == CurrentCount ? "Unchanged"
        : BaselineCount == 0 ? "Newly failing"
        : CurrentCount == 0 ? "Resolved"
        : CurrentCount < BaselineCount ? "Improved" : "Regressed";
}
