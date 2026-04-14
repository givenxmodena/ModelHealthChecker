using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace Modena.RevitAddin.Updater;

/// <summary>
/// ViewModel for the updater WPF window.
/// Mirrors the silent flow but exposes status/progress for the GUI.
/// </summary>
public class UpdateViewModel : INotifyPropertyChanged
{
    private readonly bool _updateMode;
    private readonly int? _revitPid;
    private readonly string? _revitExePath;
    private readonly Action _close;

    private string _statusMessage = "Starting updater...";
    private int _progress;
    private string _progressText = "0%";
    private bool _isIndeterminate = true;
    private bool _isRestartRevitVisible;
    private bool _isCloseEnabled;

    public int ExitCode { get; private set; }

    public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }
    public int Progress
    {
        get => _progress;
        set { if (Set(ref _progress, value)) ProgressText = $"{value}%"; }
    }
    public string ProgressText { get => _progressText; private set => Set(ref _progressText, value); }
    public bool IsIndeterminate { get => _isIndeterminate; set => Set(ref _isIndeterminate, value); }
    public bool IsRestartRevitVisible { get => _isRestartRevitVisible; set => Set(ref _isRestartRevitVisible, value); }
    public bool IsCloseEnabled { get => _isCloseEnabled; set => Set(ref _isCloseEnabled, value); }

    public ICommand CloseCommand { get; }
    public ICommand RestartRevitCommand { get; }

    public UpdateViewModel(bool updateMode, int? revitPid, string? revitExePath, Action close)
    {
        _updateMode = updateMode;
        _revitPid = revitPid;
        _revitExePath = revitExePath;
        _close = close;

        CloseCommand = new RelayCommand(_ => _close(), _ => IsCloseEnabled);
        RestartRevitCommand = new RelayCommand(_ => DoRestartRevit(), _ => IsRestartRevitVisible);
    }

    public async Task StartAsync()
    {
        try
        {
            if (_revitPid.HasValue)
            {
                StatusMessage = "Waiting for Revit to close...";
                IsIndeterminate = true;
                await Program.WaitForRevitToClose(_revitPid.Value);
            }

            if (_updateMode)
            {
                IsIndeterminate = false;
                Progress = 0;

                bool updated = await Program.CheckAndApplyUpdatesAsync(
                    revitPid: _revitPid,
                    revitExePath: _revitExePath,
                    statusCallback: msg => Dispatch(() => StatusMessage = msg),
                    progressCallback: p => Dispatch(() => { IsIndeterminate = false; Progress = p; }));

                if (updated) return; // Velopack restarts the process
            }

            StatusMessage = "Installing add-in into Revit add-ins folders...";
            IsIndeterminate = true;

            await Task.Run(Program.DeployToRevit);

            Dispatch(() =>
            {
                IsIndeterminate = false;
                Progress = 100;
                StatusMessage = string.IsNullOrEmpty(_revitExePath)
                    ? "Update complete. Please restart Revit to use the updated add-in."
                    : "Update complete. Click 'Restart Revit' to relaunch.";
                IsRestartRevitVisible = !string.IsNullOrEmpty(_revitExePath);
                IsCloseEnabled = true;
            });
        }
        catch (Exception ex)
        {
            UpdaterLogger.Log($"UpdateViewModel error: {ex}");
            Dispatch(() =>
            {
                StatusMessage = $"Error: {ex.Message}";
                IsIndeterminate = false;
                IsCloseEnabled = true;
                ExitCode = 1;
            });
        }
    }

    private void DoRestartRevit()
    {
        if (string.IsNullOrEmpty(_revitExePath)) { IsRestartRevitVisible = false; return; }
        try { Program.RestartRevit(_revitExePath); _close(); }
        catch { IsCloseEnabled = true; }
    }

    private static void Dispatch(Action action) =>
        Application.Current.Dispatcher.Invoke(action);

    // ── INotifyPropertyChanged ──────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

// Minimal relay command — avoids taking a dependency on the main add-in project
file sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? p) => canExecute?.Invoke(p) ?? true;
    public void Execute(object? p) => execute(p);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
