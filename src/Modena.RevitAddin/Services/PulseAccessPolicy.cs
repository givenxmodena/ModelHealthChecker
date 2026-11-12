using System.Windows;
using MLBS.Desktop.Client;

namespace Modena.RevitAddin.Services;

public sealed class PulseAccessPolicy : IPulseAccessPolicy
{
    private readonly IMlbsDesktopClient _client;
    private readonly IPulseTrialState _trial;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public bool IsPaidAccessActive { get; private set; }

    public PulseAccessPolicy(IMlbsDesktopClient client, IPulseTrialState trial)
    { _client = client; _trial = trial; }

    public PulseAccessDecision Initialize()
    {
        // External commands run synchronously on Revit's UI thread. Only the
        // silent network check runs off-thread; MLBS dialogs stay on the UI thread.
        var paid = Task.Run(() => _client.ValidateSigninAsync()).GetAwaiter().GetResult();
        IsPaidAccessActive = paid.IsAllowed;
        if (paid.IsAllowed) return new PulseAccessDecision(true);
        var trial = _trial.GetStatus();
        if (trial.IsActive) return new PulseAccessDecision(true, trial: true);
        _client.SignOut();
        var activation = _client.InitializeAfterTrialExpiredAsync(new MlbsTrialStatusInfo
        {
            TrialEndedAt = trial.EndDate == default ? null : new DateTimeOffset(trial.EndDate),
            UsageUsed = trial.RefreshesUsed,
            UsageLabel = "model refreshes"
        }).GetAwaiter().GetResult();
        IsPaidAccessActive = activation.IsAllowed;
        return ToDecision(activation);
    }

    public async Task<PulseAccessDecision> AuthorizeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (IsPaidAccessActive)
            {
                var paid = await _client.ValidateSigninAsync();
                IsPaidAccessActive = paid.IsAllowed;
                if (paid.IsAllowed) return new PulseAccessDecision(true);
                // Paid verification failing cannot extend or reset the local trial.
                var fallback = _trial.GetStatus();
                return fallback.IsActive
                    ? new PulseAccessDecision(true, trial: true)
                    : ToDecision(paid);
            }
            var trial = _trial.GetStatus();
            return new PulseAccessDecision(trial.IsActive, trial.IsActive, trial.Message);
        }
        finally { _gate.Release(); }
    }

    public async Task<PulseAccessDecision> ActivateAsync(Window? owner = null)
    {
        await _gate.WaitAsync();
        try
        {
            var result = await _client.InitializeAsync(owner: owner);
            IsPaidAccessActive = result.IsAllowed;
            return ToDecision(result);
        }
        finally { _gate.Release(); }
    }

    public async Task ShowProfileAsync(Window owner)
    {
        await _gate.WaitAsync();
        try
        {
            if (await _client.ShowProfileAsync(owner)) IsPaidAccessActive = false;
        }
        finally { _gate.Release(); }
    }

    // The mode at authorization determines accounting, even if activation happens
    // while an extraction is awaiting completion. Paid refreshes have no extra charge.
    public void RecordSuccessfulRefresh(bool wasTrial)
    { if (wasTrial) _trial.RecordRefresh(); }

    private static PulseAccessDecision ToDecision(MlbsAccessResult result) =>
        new(result.IsAllowed, message: result.IsAllowed ? "" :
            result.Status == MlbsResultStatus.Cancelled ? "Pulse activation was cancelled." :
            "Pulse could not verify your licence. Sign in or check your connection and try again.");

    public void Dispose() { if (_client is IDisposable disposable) disposable.Dispose(); }
}
