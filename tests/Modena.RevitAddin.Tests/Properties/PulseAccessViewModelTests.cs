using Modena.RevitAddin.RevitApi;
using Modena.RevitAddin.Services;
using Modena.RevitAddin.ViewModels;
using Modena.Shared.DTOs;
using Moq;

namespace Modena.RevitAddin.Tests.Properties;

public class PulseAccessViewModelTests
{
    [Fact]
    public Task DeniedAccessBlocksRefreshAndFamilyExtraction() => HealthHistoryTests.OnDispatcher(async () =>
    {
        var access = new Mock<IPulseAccessPolicy>();
        access.Setup(a => a.AuthorizeAsync()).ReturnsAsync(new PulseAccessDecision(false, message: "Activation required"));
        var extractor = new Mock<IModelHealthExtractor>(MockBehavior.Strict);
        var vm = new ModelHealthViewModel(new ModelIdentity(), extractor.Object,
            Mock.Of<IRevitDocumentContext>(), new PluginConfig { AutoRefreshEnabled = true }, accessPolicy: access.Object);
        try
        {
            await vm.RefreshCommand.ExecuteAsync();
            await vm.LoadFamilySizesCommand.ExecuteAsync();
            extractor.VerifyNoOtherCalls();
            Assert.False(vm.IsTimerRunning);
            Assert.Equal("Activation required", vm.ErrorMessage);
            access.Verify(a => a.RecordSuccessfulRefresh(It.IsAny<bool>()), Times.Never);
        }
        finally { vm.Cleanup(); }
    });

    [Fact]
    public Task ClosingDuringLicenceCheckCannotStartExtraction() => HealthHistoryTests.OnDispatcher(async () =>
    {
        var completion = new TaskCompletionSource<PulseAccessDecision>();
        var access = new Mock<IPulseAccessPolicy>();
        access.Setup(a => a.AuthorizeAsync()).Returns(completion.Task);
        var extractor = new Mock<IModelHealthExtractor>(MockBehavior.Strict);
        var vm = new ModelHealthViewModel(new ModelIdentity(), extractor.Object,
            Mock.Of<IRevitDocumentContext>(), new PluginConfig(), accessPolicy: access.Object);
        var refresh = vm.RefreshCommand.ExecuteAsync();
        vm.Cleanup();
        completion.SetResult(new PulseAccessDecision(true));
        await refresh;
        extractor.VerifyNoOtherCalls();
        access.Verify(a => a.Dispose(), Times.Once);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task OnlySuccessfulRefreshesAreAccounted(bool fails) => HealthHistoryTests.OnDispatcher(async () =>
    {
        var access = new Mock<IPulseAccessPolicy>();
        access.Setup(a => a.AuthorizeAsync()).ReturnsAsync(new PulseAccessDecision(true, trial: true));
        var extractor = new Mock<IModelHealthExtractor>();
        if (fails)
            extractor.Setup(e => e.ExtractFastAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Failed scan"));
        else
        {
            extractor.Setup(e => e.ExtractFastAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new DashboardResponse());
            extractor.Setup(e => e.ExtractFamilySizesAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<FamilyDto>());
        }
        var vm = new ModelHealthViewModel(new ModelIdentity(), extractor.Object,
            Mock.Of<IRevitDocumentContext>(), new PluginConfig { AutoRefreshEnabled = false }, accessPolicy: access.Object);
        try
        {
            await vm.RefreshCommand.ExecuteAsync();
            access.Verify(a => a.RecordSuccessfulRefresh(true), fails ? Times.Never() : Times.Once());
        }
        finally { vm.Cleanup(); }
    });
}
