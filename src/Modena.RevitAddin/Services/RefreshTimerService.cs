using System.Windows.Threading;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Wraps a DispatcherTimer with a built-in reentrancy guard.
/// If the callback is still running when the next tick fires, the tick is skipped.
/// </summary>
public class RefreshTimerService : IDisposable
{
    private DispatcherTimer? _timer;
    private Func<Task>? _callback;
    private volatile bool _isCallbackRunning;
    private bool _disposed;

    public bool IsRunning => _timer?.IsEnabled == true;

    public void Start(TimeSpan interval, Func<Task> callback)
    {
        Stop();

        _callback = callback;
        _timer = new DispatcherTimer
        {
            Interval = interval
        };
        _timer.Tick += OnTick;
        _timer.Start();

        LogService.Info($"Refresh timer started with interval {interval.TotalMinutes} minutes.");
    }

    public void Stop()
    {
        if (_timer is null) return;

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
        _callback = null;

        LogService.Info("Refresh timer stopped.");
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (_isCallbackRunning)
        {
            LogService.Debug("Timer tick skipped — callback still running.");
            return;
        }

        _isCallbackRunning = true;
        try
        {
            if (_callback is not null)
                await _callback();
        }
        catch (Exception ex)
        {
            LogService.Error("Timer callback failed.", ex);
        }
        finally
        {
            _isCallbackRunning = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
