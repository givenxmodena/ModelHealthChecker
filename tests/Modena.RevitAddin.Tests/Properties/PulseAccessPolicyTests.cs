using MLBS.Desktop.Client;
using Modena.RevitAddin.Services;
using Moq;

namespace Modena.RevitAddin.Tests.Properties;

public class PulseAccessPolicyTests
{
    private readonly Mock<IMlbsDesktopClient> _client = new();
    private readonly Mock<IPulseTrialState> _trial = new();
    private static MlbsAccessResult Paid() => MlbsAccessResult.Allowed(new MlbsSessionInfo());
    private static MlbsAccessResult Denied() => MlbsAccessResult.Denied(MlbsResultStatus.Unavailable, "offline");

    private PulseAccessPolicy Create(bool trialActive, bool paid)
    {
        _trial.Setup(t => t.GetStatus()).Returns(new PulseTrialInfo { IsActive = trialActive, RefreshesUsed = 17 });
        _client.Setup(c => c.ValidateSigninAsync(default)).ReturnsAsync(paid ? Paid() : Denied());
        _client.Setup(c => c.InitializeAfterTrialExpiredAsync(It.IsAny<MlbsTrialStatusInfo>(), null, null, default)).ReturnsAsync(Denied());
        return new PulseAccessPolicy(_client.Object, _trial.Object);
    }

    [Fact]
    public void ValidPaidAccessBypassesExpiredTrial()
    {
        using var policy = Create(false, true);
        var access = policy.Initialize();
        Assert.True(access.IsAllowed);
        Assert.False(access.IsTrial);
        Assert.True(policy.IsPaidAccessActive);
        _trial.Verify(t => t.GetStatus(), Times.Never);
    }

    [Fact]
    public void ActiveTrialContinuesDuringMlbsOutageWithoutDialog()
    {
        using var policy = Create(true, false);
        var access = policy.Initialize();
        Assert.True(access.IsAllowed);
        Assert.True(access.IsTrial);
        _client.Verify(c => c.InitializeAfterTrialExpiredAsync(It.IsAny<MlbsTrialStatusInfo>(), null, null, default), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExpiredTrialRequiresSuccessfulActivation(bool activated)
    {
        using var policy = Create(false, false);
        _client.Setup(c => c.InitializeAfterTrialExpiredAsync(It.Is<MlbsTrialStatusInfo>(s => s.UsageUsed == 17 && s.UsageLabel == "model refreshes"), null, null, default))
            .ReturnsAsync(activated ? Paid() : Denied());
        Assert.Equal(activated, policy.Initialize().IsAllowed);
        Assert.Equal(activated, policy.IsPaidAccessActive);
        _client.Verify(c => c.SignOut(), Times.Once);
    }

    [Fact]
    public async Task PaidRefreshIsRevalidatedAndDeniedDuringOutageAfterTrial()
    {
        using var policy = Create(false, true);
        policy.Initialize();
        _client.Setup(c => c.ValidateSigninAsync(default)).ReturnsAsync(Denied());
        Assert.False((await policy.AuthorizeAsync()).IsAllowed);
        Assert.False(policy.IsPaidAccessActive);
    }

    [Fact]
    public async Task OpenDashboardTrialExpiryBlocksRefreshWithoutPrompting()
    {
        using var policy = Create(true, false);
        policy.Initialize();
        _trial.Setup(t => t.GetStatus()).Returns(new PulseTrialInfo { IsActive = false });
        Assert.False((await policy.AuthorizeAsync()).IsAllowed);
        _client.Verify(c => c.InitializeAsync(null, null, default), Times.Never);
    }

    [Fact]
    public async Task ActivationSwitchesOpenDashboardToPaidWithoutTrialAccounting()
    {
        using var policy = Create(true, false);
        policy.Initialize();
        _client.Setup(c => c.InitializeAsync(null, null, default)).ReturnsAsync(Paid());
        Assert.True((await policy.ActivateAsync()).IsAllowed);
        Assert.True(policy.IsPaidAccessActive);
        policy.RecordSuccessfulRefresh(false);
        _trial.Verify(t => t.RecordRefresh(), Times.Never);
        _client.Verify(c => c.RecordUsageAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task SignOutRemovesPaidAccessAndExpiredTrialCannotContinue()
    {
        using var policy = Create(false, true);
        policy.Initialize();
        _client.Setup(c => c.ShowProfileAsync(null, default)).ReturnsAsync(true);
        await policy.ShowProfileAsync(null!);
        Assert.False(policy.IsPaidAccessActive);
        Assert.False((await policy.AuthorizeAsync()).IsAllowed);
    }

    [Fact]
    public async Task TrialRefreshStillCountsIfActivationHappensDuringExtraction()
    {
        using var policy = Create(true, false);
        policy.Initialize();
        var authorization = await policy.AuthorizeAsync();
        _client.Setup(c => c.InitializeAsync(null, null, default)).ReturnsAsync(Paid());
        await policy.ActivateAsync();
        policy.RecordSuccessfulRefresh(authorization.IsTrial);
        _trial.Verify(t => t.RecordRefresh(), Times.Once);
    }
}
