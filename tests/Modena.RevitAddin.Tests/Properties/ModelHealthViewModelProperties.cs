using FsCheck;
using FsCheck.Xunit;
using Moq;
using Modena.RevitAddin.RevitApi;
using Modena.RevitAddin.Services;
using Modena.RevitAddin.ViewModels;
using Modena.RevitAddin.Tests.Generators;
using Modena.Shared.DTOs;
using System.Windows.Threading;

namespace Modena.RevitAddin.Tests.Properties;

/// <summary>
/// Property-based tests for ModelHealthViewModel.
/// </summary>
public class ModelHealthViewModelProperties
{
    /// <summary>
    /// Creates a ViewModel with a mocked extractor that returns the given response.
    /// Uses the current thread's Dispatcher and a real RefreshTimerService.
    /// </summary>
    private static (ModelHealthViewModel vm, Mock<IModelHealthExtractor> mockExtractor, RefreshTimerService timer)
        CreateViewModel(
            DashboardResponse? extractResult,
            PluginConfig? config = null,
            Exception? extractException = null)
    {
        var identity = new ModelIdentity
        {
            DocumentTitle = "TestDoc",
            ModelPath = @"C:\test\model.rvt",
            RevitVersion = "2024",
            IsCloudModel = false,
            ModelSource = Modena.Shared.Enums.ModelSource.Local
        };

        var mockExtractor = new Mock<IModelHealthExtractor>();
        if (extractException is not null)
        {
            mockExtractor
                .Setup(e => e.ExtractAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(extractException);
        }
        else
        {
            mockExtractor
                .Setup(e => e.ExtractAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(extractResult!);
        }

        var mockDocContext = new Mock<IRevitDocumentContext>();
        mockDocContext.Setup(d => d.HasActiveDocument).Returns(true);
        mockDocContext.Setup(d => d.DocumentTitle).Returns("TestDoc");

        var cfg = config ?? new PluginConfig { AutoRefreshEnabled = true, RefreshIntervalMinutes = 5 };
        var timer = new RefreshTimerService();
        var dispatcher = Dispatcher.CurrentDispatcher;

        var vm = new ModelHealthViewModel(identity, mockExtractor.Object, mockDocContext.Object, cfg, dispatcher, timer);
        return (vm, mockExtractor, timer);
    }

    /// <summary>
    /// Runs an async action on the Dispatcher and pumps the message queue until complete.
    /// </summary>
    private static void RunOnDispatcher(Action action)
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(() =>
        {
            action();
            frame.Continue = false;
        });
        Dispatcher.PushFrame(frame);
    }

    private static void RunOnDispatcherAsync(Func<Task> action)
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
        {
            await action();
            frame.Continue = false;
        });
        Dispatcher.PushFrame(frame);
    }

    // Feature: modena-model-health-checker, Property 1: Successful load populates ViewModel state
    // **Validates: Requirements 3.1, 3.2**
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DtoArbitraryProvider) })]
    public bool SuccessfulLoad_PopulatesViewModelState(DashboardResponse response)
    {
        var (vm, _, timer) = CreateViewModel(response, new PluginConfig { AutoRefreshEnabled = false });

        RunOnDispatcherAsync(async () =>
        {
            await vm.LoadCommand.ExecuteAsync();
        });

        var pass = !vm.IsLoading
            && vm.HasData
            && (vm.ErrorMessage is null || vm.ErrorMessage == string.Empty)
            && !string.IsNullOrEmpty(vm.LastRefreshedText)
            && vm.ModelName == response.ModelName
            && vm.ProjectName == response.ProjectName
            && vm.FailedChecks.Count == response.FailedChecks.Count
            && vm.Metrics.Count == response.Metrics.Count
            && vm.Categories.Count == response.Categories.Count
            && vm.Families.Count == response.Families.Count
            && vm.PassedChecks.Count == response.PassedChecks.Count;

        vm.Cleanup();
        timer.Dispose();
        return pass;
    }

    // Feature: modena-model-health-checker, Property 2: Failed load sets error state
    // **Validates: Requirements 3.3**
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DtoArbitraryProvider) })]
    public bool FailedLoad_SetsErrorState(NonEmptyString errorMsg)
    {
        var (vm, _, timer) = CreateViewModel(
            null,
            new PluginConfig { AutoRefreshEnabled = false },
            extractException: new Exception(errorMsg.Get));

        RunOnDispatcherAsync(async () =>
        {
            await vm.LoadCommand.ExecuteAsync();
        });

        var pass = !vm.IsLoading
            && !string.IsNullOrEmpty(vm.ErrorMessage);

        vm.Cleanup();
        timer.Dispose();
        return pass;
    }

    // Feature: modena-model-health-checker, Property 3: Concurrent refreshes are prevented
    // **Validates: Requirements 3.4, 4.2**
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DtoArbitraryProvider) })]
    public bool ConcurrentRefreshes_ArePrevented(DashboardResponse response)
    {
        var callCount = 0;
        var tcs = new TaskCompletionSource<DashboardResponse>();

        var identity = new ModelIdentity
        {
            DocumentTitle = "TestDoc",
            ModelPath = @"C:\test\model.rvt",
            RevitVersion = "2024",
            IsCloudModel = false,
            ModelSource = Modena.Shared.Enums.ModelSource.Local
        };

        var mockExtractor = new Mock<IModelHealthExtractor>();
        mockExtractor
            .Setup(e => e.ExtractAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref callCount);
                return tcs.Task;
            });

        var mockDocContext = new Mock<IRevitDocumentContext>();
        mockDocContext.Setup(d => d.HasActiveDocument).Returns(true);

        var cfg = new PluginConfig { AutoRefreshEnabled = false };
        var timer = new RefreshTimerService();
        var vm = new ModelHealthViewModel(identity, mockExtractor.Object, mockDocContext.Object, cfg, Dispatcher.CurrentDispatcher, timer);

        // Start first load (will block on tcs)
        RunOnDispatcher(() =>
        {
            vm.LoadCommand.Execute(null);
        });

        // Attempt second load while first is in progress
        RunOnDispatcher(() =>
        {
            vm.LoadCommand.Execute(null);
        });

        // Complete the first load
        tcs.SetResult(response);

        // Pump dispatcher to let completion handlers run
        RunOnDispatcher(() => { });

        // Only one extraction call should have been made
        var pass = callCount == 1;

        vm.Cleanup();
        timer.Dispose();
        return pass;
    }

    // Feature: modena-model-health-checker, Property 4: Timer starts after first successful load
    // **Validates: Requirements 4.1**
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DtoArbitraryProvider) })]
    public bool TimerStartsAfterFirstSuccessfulLoad(DashboardResponse response)
    {
        var cfg = new PluginConfig { AutoRefreshEnabled = true, RefreshIntervalMinutes = 5 };
        var (vm, _, timer) = CreateViewModel(response, cfg);

        RunOnDispatcherAsync(async () =>
        {
            await vm.LoadCommand.ExecuteAsync();
        });

        var pass = vm.IsTimerRunning;

        vm.Cleanup();
        timer.Dispose();
        return pass;
    }

    // Feature: modena-model-health-checker, Property 5: Timer stops on cleanup
    // **Validates: Requirements 4.3**
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DtoArbitraryProvider) })]
    public bool TimerStopsOnCleanup(DashboardResponse response)
    {
        var cfg = new PluginConfig { AutoRefreshEnabled = true, RefreshIntervalMinutes = 5 };
        var (vm, _, timer) = CreateViewModel(response, cfg);

        RunOnDispatcherAsync(async () =>
        {
            await vm.LoadCommand.ExecuteAsync();
        });

        // Timer should be running after successful load
        var wasRunning = vm.IsTimerRunning;

        // Cleanup should stop the timer
        vm.Cleanup();

        var pass = wasRunning && !vm.IsTimerRunning;

        timer.Dispose();
        return pass;
    }
}
