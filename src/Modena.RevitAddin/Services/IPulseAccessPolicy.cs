using System.Windows;

namespace Modena.RevitAddin.Services;

public sealed class PulseAccessDecision
{
    public bool IsAllowed { get; }
    public bool IsTrial { get; }
    public string Message { get; }
    public PulseAccessDecision(bool allowed, bool trial = false, string message = "")
    { IsAllowed = allowed; IsTrial = trial; Message = message; }
}

public sealed class PulseTrialInfo
{
    public bool IsActive { get; set; }
    public DateTime EndDate { get; set; }
    public int RefreshesUsed { get; set; }
    public string Message { get; set; } = "Your Pulse trial has expired. Sign in to activate your licence.";
}

public interface IPulseTrialState
{
    PulseTrialInfo GetStatus();
    void RecordRefresh();
}

/// <summary>Pulse owns trial rules; MLBS owns paid access and account presentation.</summary>
public interface IPulseAccessPolicy : IDisposable
{
    bool IsPaidAccessActive { get; }
    PulseAccessDecision Initialize();
    Task<PulseAccessDecision> AuthorizeAsync();
    Task<PulseAccessDecision> ActivateAsync(Window? owner = null);
    Task ShowProfileAsync(Window owner);
    void RecordSuccessfulRefresh(bool wasTrial);
}
