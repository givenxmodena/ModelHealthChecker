using System.IO;
using System.Windows.Threading;
using Moq;
using Modena.RevitAddin.RevitApi;
using Modena.RevitAddin.Services;
using Modena.RevitAddin.ViewModels;
using Modena.Shared.DTOs;

namespace Modena.RevitAddin.Tests.Properties;

public sealed class HealthHistoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PulseHistoryTests", Guid.NewGuid().ToString("N"));
    private HealthHistoryService Service => new(_directory);

    private static HealthScanRecord Scan(int rate = 40, string key = "model") => new()
    {
        ModelKey = key, Timestamp = DateTime.UtcNow, PassRate = rate,
        TotalChecks = 3, PassedChecks = 1, FailedChecks = 2,
        PassedCheckNames = new List<string> { "Rooms" },
        FailedCheckSummaries = new List<HealthCheckSummary>
        {
            new() { Name = "Mirrors", Count = 4, Category = "Quality", Priority = "MEDIUM" },
            new() { Name = "Names", Count = 9, Category = "Naming", Priority = "LOW" }
        }
    };

    [Fact]
    public void HealthHistory_RetainsIdenticalResultsAndOrdersByTimestamp()
    {
        var later = Scan();
        var earlier = Scan();
        earlier.Timestamp = later.Timestamp.AddHours(-1);
        Assert.True(Service.Append("model", later));
        Assert.True(Service.Append("model", earlier));
        Assert.True(Service.Append("model", later));
        var loaded = Service.LoadHistory("model");
        Assert.Equal(3, loaded.Count);
        Assert.Equal(earlier.Id, loaded[0].Id);
        Assert.All(loaded, r => Assert.Equal(DateTimeKind.Utc, r.Timestamp.Kind));
    }

    [Fact]
    public void HealthHistory_RecoversAfterMalformedAndInterruptedLines()
    {
        Assert.True(Service.Append("model", Scan()));
        var path = Directory.GetFiles(_directory).Single();
        File.AppendAllText(path, "null\n{broken json\n{\"ModelKey\":");
        Assert.True(Service.Append("model", Scan(60)));
        Assert.Equal(new[] { 40, 60 }, Service.LoadHistory("model").Select(s => s.PassRate));
    }

    [Fact]
    public async Task HealthHistory_SerializesConcurrentWriters()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => Task.Run(() => Service.Append("model", Scan()))));
        Assert.All(results, Assert.True);
        Assert.Equal(30, Service.LoadHistory("model").Count);
    }

    [Fact]
    public void HealthHistory_UnsafeAndLongKeysStayIsolatedWithinDirectory()
    {
        var keys = new[] { "../model/a", "..\\model\\a", new string('a', 1000) };
        foreach (var key in keys) Assert.True(Service.Append(key, Scan(key: key)));
        Assert.Equal(3, Directory.GetFiles(_directory, "*.jsonl").Length);
        foreach (var key in keys) Assert.Single(Service.LoadHistory(key));
    }

    [Fact]
    public void HealthHistory_StorageFailureDoesNotThrowOrClaimSuccess()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "a-file");
        File.WriteAllText(file, "occupied");
        Assert.False(new HealthHistoryService(file).Append("model", Scan()));
        Assert.False(Service.Append("other", Scan()));
    }

    [Fact]
    public void HealthHistory_RecordOmitsHeavyAndNavigableDetails()
    {
        var response = new DashboardResponse
        {
            ModelKey = "model", LastUpdatedUtc = DateTime.UtcNow,
            FailedChecks = new List<FailedCheckDto>
            {
                new() { Name = "Mirrors", Count = 2, ElementIds = new List<long> { 123 }, Description = "Details", Recommendation = "Fix" }
            },
            PassedChecks = new List<string> { "Rooms" }
        };
        Assert.True(Service.Append("model", HealthScanRecord.FromResponse(response)));
        var json = File.ReadAllText(Directory.GetFiles(_directory).Single());
        Assert.DoesNotContain("ElementIds", json);
        Assert.DoesNotContain("Description", json);
        Assert.DoesNotContain("Recommendation", json);
        Assert.Contains("Rooms", json);
    }

    [Fact]
    public void HealthHistory_ComparisonDistinguishesMissingAndAllChangeTypes()
    {
        var before = Scan();
        before.PassedCheckNames.Add("Stable");
        before.FailedCheckSummaries.Add(new HealthCheckSummary { Name = "Resolved", Count = 1 });
        before.FailedCheckSummaries.Add(new HealthCheckSummary { Name = "Regressed", Count = 1 });
        var after = Scan(60);
        after.PassedCheckNames = new List<string> { "Mirrors", "Resolved", "Stable", "Added" };
        after.FailedCheckSummaries = new List<HealthCheckSummary>
        {
            new() { Name = "Rooms", Count = 3 }, new() { Name = "Names", Count = 4 },
            new() { Name = "Regressed", Count = 5 }
        };
        var changes = HistoryViewModel.Compare(before, after).ToDictionary(c => c.Name);
        Assert.Equal("Resolved", changes["Mirrors"].Status);
        Assert.Equal("Resolved", changes["Resolved"].Status);
        Assert.Equal("Newly failing", changes["Rooms"].Status);
        Assert.Equal("Improved", changes["Names"].Status);
        Assert.Equal("Regressed", changes["Regressed"].Status);
        Assert.Equal("Unchanged", changes["Stable"].Status);
        Assert.Equal("Not comparable", changes["Added"].Status);
        Assert.Null(changes["Added"].BaselineCount);
        Assert.Equal("Not comparable", HistoryViewModel.Compare(after, before).Single(c => c.Name == "Added").Status);
        Assert.Equal(new[] { "Newly failing", "Regressed", "Resolved", "Resolved", "Improved", "Not comparable", "Unchanged" },
            HistoryViewModel.Compare(before, after).Select(c => c.Status));
    }

    [Fact]
    public async Task HealthHistory_FiltersAndSummaryAccountForChangesWithoutChangingSelections()
    {
        var service = Service;
        var baseline = Scan();
        baseline.PassedCheckNames.Add("Stable");
        baseline.FailedCheckSummaries.Add(new HealthCheckSummary { Name = "Removed", Count = 2 });
        var current = Scan(60);
        current.Timestamp = baseline.Timestamp.AddMinutes(5);
        current.PassedCheckNames = new List<string> { "Mirrors", "Stable", "Added" };
        current.FailedCheckSummaries = new List<HealthCheckSummary>
        {
            new() { Name = "Rooms", Count = 3 }, new() { Name = "Names", Count = 4 }
        };
        service.Append("model", baseline);
        service.Append("model", current);
        var vm = new HistoryViewModel(service);
        await vm.LoadAsync("model");
        Assert.Equal(CheckChangeFilter.Changed, vm.SelectedChangeFilter);
        Assert.Equal(5, vm.Changes.Count);
        Assert.Equal("1 check started failing; 1 check now passes; 1 existing issue improved; 2 checks cannot be compared.", vm.ComparisonSummary);
        Assert.Contains(baseline.Timestamp.ToLocalTime().ToString("HH:mm:ss"), vm.BaselineCaption);
        Assert.Contains(current.Timestamp.ToLocalTime().ToString("HH:mm:ss"), vm.CurrentCaption);

        vm.SelectedChangeFilter = CheckChangeFilter.Worsened;
        Assert.Equal("Newly failing", Assert.Single(vm.Changes).Status);
        vm.SelectedChangeFilter = CheckChangeFilter.Improved;
        Assert.Equal(new[] { "Resolved", "Improved" }, vm.Changes.Select(c => c.Status));
        vm.SelectedChangeFilter = CheckChangeFilter.All;
        Assert.Equal(6, vm.Changes.Count);
        Assert.Equal("Unchanged", vm.Changes.Last().Status);
        Assert.Equal(baseline.Id, vm.Baseline!.Record.Id);
        Assert.Equal(current.Id, vm.Current!.Record.Id);
        await vm.LoadAsync("model");
        Assert.Equal(CheckChangeFilter.All, vm.SelectedChangeFilter);

        vm.Current = vm.Baseline;
        vm.SelectedChangeFilter = CheckChangeFilter.Worsened;
        Assert.Empty(vm.Changes);
        Assert.Equal("No check results changed.", vm.ComparisonSummary);
        Assert.Equal("No checks worsened between these scans.", vm.EmptyChangesMessage);
        vm.SelectedChangeFilter = CheckChangeFilter.Improved;
        Assert.Empty(vm.Changes);
        Assert.Equal("No checks improved between these scans.", vm.EmptyChangesMessage);
    }

    [Fact]
    public async Task HealthHistory_EmptyRecordedCheckSetsDoNotClaimUnchangedResults()
    {
        var service = Service;
        for (var i = 0; i < 2; i++)
        {
            var scan = Scan();
            scan.PassedCheckNames.Clear();
            scan.FailedCheckSummaries.Clear();
            service.Append("model", scan);
        }
        var vm = new HistoryViewModel(service);
        await vm.LoadAsync("model");
        Assert.Equal("No evaluated checks were recorded for these scans.", vm.ComparisonSummary);
        foreach (var filter in vm.ChangeFilters)
        {
            vm.SelectedChangeFilter = filter;
            Assert.Empty(vm.Changes);
            Assert.Equal("No evaluated checks were recorded for these scans.", vm.EmptyChangesMessage);
        }
    }

    [Fact]
    public async Task HealthHistory_SelectionDefaultsAndManualSelectionsSurviveReload()
    {
        var service = Service;
        var vm = new HistoryViewModel(service);
        await vm.LoadAsync("model");
        Assert.False(vm.HasHistory);
        Assert.False(vm.CanCompare);
        Assert.Null(vm.Baseline);
        var first = Scan(60);
        service.Append("model", first);
        await vm.LoadAsync("model");
        Assert.False(vm.CanCompare);
        Assert.Null(vm.Baseline);
        service.Append("model", Scan(40));
        await vm.LoadAsync("model");
        Assert.Equal(first.Id, vm.Baseline!.Record.Id);
        Assert.Equal("-20 pp", vm.ComparisonDelta);
        Assert.Equal("60%", vm.BestPassRate);
        Assert.True(vm.Scans[1].IsBaseline);
        Assert.True(vm.Scans[0].IsCurrent);
        vm.Current = vm.Scans[1];
        var manualCurrent = vm.Current.Record.Id;
        service.Append("model", Scan(80));
        await vm.LoadAsync("model");
        Assert.Equal(manualCurrent, vm.Current!.Record.Id);
        vm.ComparePreviousCommand.Execute(null);
        Assert.Equal(80, vm.Current.Record.PassRate);
        Assert.Equal(40, vm.Baseline!.Record.PassRate);
        vm.CompareBestCommand.Execute(null);
        Assert.Equal(80, vm.Baseline.Record.PassRate);
    }

    [Fact]
    public void HealthHistory_UnknownVersionsWrongModelsAndInvalidCollectionsAreSkipped()
    {
        Service.Append("model", Scan());
        var path = Directory.GetFiles(_directory).Single();
        var json = File.ReadAllText(path);
        File.AppendAllText(path, json.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":2"));
        File.AppendAllText(path, json.Replace("\"ModelKey\":\"model\"", "\"ModelKey\":\"other\""));
        File.AppendAllText(path, json.Replace("\"PassedCheckNames\":[\"Rooms\"]", "\"PassedCheckNames\":null"));
        Assert.Single(Service.LoadHistory("model"));
    }

    [Fact]
    public async Task HealthHistory_OnlyCompletedScansAppendIncludingSilentCacheRefresh()
    {
        var key = "history-test-" + Guid.NewGuid().ToString("N");
        var extractor = new Mock<IModelHealthExtractor>();
        var response = new DashboardResponse
        {
            ModelKey = key, ModelName = "Test", LastUpdatedUtc = DateTime.UtcNow,
            Summary = new SummaryDto { TotalChecks = 1, PassedChecks = 1, PassRate = 100 },
            PassedChecks = new List<string> { "Rooms" }
        };
        extractor.Setup(e => e.ExtractFastAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
        extractor.Setup(e => e.ExtractFamilySizesAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<FamilyDto>());
        var context = new Mock<IRevitDocumentContext>();
        context.SetupGet(c => c.IsCloudModel).Returns(true);
        context.SetupGet(c => c.ProjectGuid).Returns("history");
        context.SetupGet(c => c.ModelGuid).Returns(key.Substring("history-".Length));
        var identity = new ModelIdentity { IsCloudModel = true, ProjectGuid = "history", ModelGuid = key.Substring("history-".Length) };
        ModelHealthViewModel NewViewModel() => new(identity, extractor.Object, context.Object,
            new PluginConfig { AutoRefreshEnabled = false }, historyService: Service);
        var cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Modena", "cache", key + ".json");
        try
        {
            await OnDispatcher(async () =>
            {
                var vm = NewViewModel();
                await vm.InitializeAsync();
                Assert.Single(Service.LoadHistory(key));
                await vm.RefreshCommand.ExecuteAsync();
                Assert.Equal(2, Service.LoadHistory(key).Count);
                extractor.Setup(e => e.ExtractFamilySizesAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new OperationCanceledException());
                await vm.RefreshCommand.ExecuteAsync();
                Assert.Equal(2, Service.LoadHistory(key).Count);
                vm.Cleanup();

                // Cached restoration followed by a failed silent scan adds no historical record.
                extractor.Setup(e => e.ExtractFastAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new IOException("Scan failed"));
                vm = NewViewModel();
                await vm.InitializeAsync();
                Assert.True(vm.HasData);
                Assert.Equal(2, Service.LoadHistory(key).Count);
                vm.Cleanup();

                // A successful silent refresh appends exactly once, not once per phase/cache load.
                extractor.Setup(e => e.ExtractFastAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
                extractor.Setup(e => e.ExtractFamilySizesAsync(It.IsAny<IRevitDocumentContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<FamilyDto>());
                vm = NewViewModel();
                await vm.InitializeAsync();
                Assert.Equal(3, Service.LoadHistory(key).Count);
                vm.Cleanup();
            });
        }
        finally { if (File.Exists(cachePath)) File.Delete(cachePath); }
    }

    internal static Task OnDispatcher(Func<Task> action)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(async () =>
            {
                try { await action(); completion.SetResult(true); }
                catch (Exception ex) { completion.SetException(ex); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
