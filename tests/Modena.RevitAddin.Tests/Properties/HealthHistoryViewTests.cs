using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Moq;
using Modena.RevitAddin.RevitApi;
using Modena.RevitAddin.Services;
using Modena.RevitAddin.ViewModels;
using Modena.RevitAddin.Views;
using Modena.Shared.DTOs;

namespace Modena.RevitAddin.Tests.Properties;

public sealed class HealthHistoryViewTests
{
    [Fact]
    public Task HealthHistory_ActualWpfViewsRenderAndKeepBoundSelections() => HealthHistoryTests.OnDispatcher(async () =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "PulseHistoryViewTests", Guid.NewGuid().ToString("N"));
        var trace = new BindingTrace();
        PresentationTraceSources.DataBindingSource.Listeners.Add(trace);
        var oldLevel = PresentationTraceSources.DataBindingSource.Switch.Level;
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var service = new HealthHistoryService(directory);
        var context = new Mock<IRevitDocumentContext>();
        context.SetupGet(c => c.DocumentTitle).Returns("Snowdon Towers Sample Structural");
        context.SetupGet(c => c.ModelPath).Returns(@"C:\Samples\Snowdon.rvt");
        var vm = new ModelHealthViewModel(new ModelIdentity(), new Mock<IModelHealthExtractor>().Object,
            context.Object, new PluginConfig { AutoRefreshEnabled = false }, historyService: service);
        ModelHealthWindow? window = null;
        try
        {
            var rates = new[] { 27, 33, 33, 40, 40, 47, 53, 60, 53, 40 };
            for (var i = 0; i < rates.Length; i++)
            {
                service.Append("model", new HealthScanRecord
                {
                    ModelKey = "model", Timestamp = new DateTime(2026, 6, 2, 8, 4, 0, DateTimeKind.Utc).AddDays(7 * i),
                    PassRate = rates[i], TotalChecks = 15, PassedChecks = (int)Math.Round(rates[i] * 15 / 100.0),
                    WarningsCount = 2, ModelElements = 5100 + 26 * i,
                    PassedCheckNames = new List<string> { "Stable check", "Rooms" },
                    FailedCheckSummaries = new List<HealthCheckSummary>
                    {
                        new() { Name = "Mirrored Elements", Count = i == 9 ? 23 : 0, Category = "Model Quality", Priority = "MEDIUM" },
                        new() { Name = "Unplaced Rooms", Count = i == 9 ? 5 : 0, Category = "Model Quality", Priority = "MEDIUM" },
                        new() { Name = "Duplicate Type Names", Count = i == 9 ? 4 : 9, Category = "Naming", Priority = "LOW" }
                    }
                });
            }
            await vm.History.LoadAsync("model");
            window = new ModelHealthWindow(vm);
            var root = (FrameworkElement)window.Content;
            ((Panel)root).Background = window.Background;
            var tabs = (TabControl)window.FindName("MainTabs");
            Assert.Equal(2, tabs.Items.Count);
            Assert.Equal("Dashboard", ((TabItem)tabs.SelectedItem).Header);
            tabs.SelectedIndex = 1;
            var history = (HistoryView)((TabItem)tabs.Items[1]).Content;
            Render(root, 1080, 1050, "history-default");
            var tiles = (Panel)history.FindName("SummaryTiles");
            Assert.Equal(4, tiles.Children.Count);
            var badge = (Border)history.FindName("LatestDeltaBadge");
            var badgeText = Assert.Single(Descendants<TextBlock>(badge));
            Assert.Equal("▼ -13 pp", badgeText.Text);
            Assert.Equal(Color.FromRgb(0xf4, 0x3f, 0x5e), ((SolidColorBrush)badgeText.Foreground).Color);
            Assert.Contains(Descendants<TextBlock>(tiles), t => t.Text == "-20 pp");
            var comparisonPanel = (FrameworkElement)history.FindName("ComparisonPanel");
            var scanPanel = (FrameworkElement)history.FindName("ScanPanel");
            var scanGrid = (DataGrid)history.FindName("ScanGrid");
            Assert.Equal(0, Grid.GetRow(comparisonPanel));
            Assert.Equal(1, Grid.GetRow(scanPanel));
            Assert.Equal(240, scanGrid.Height);
            Assert.True(comparisonPanel.TransformToAncestor(root).Transform(new Point()).Y < scanPanel.TransformToAncestor(root).Transform(new Point()).Y);
            Assert.Contains(Descendants<TextBlock>(comparisonPanel), t => t.Text == vm.History.ComparisonSummary);
            Assert.Contains(Descendants<TextBlock>(comparisonPanel), t => t.Text == vm.History.BaselineCaption);
            var filter = (ListBox)history.FindName("ChangeFilter");
            filter.SelectedItem = CheckChangeFilter.Worsened;
            root.UpdateLayout();
            Assert.Equal(CheckChangeFilter.Worsened, vm.History.SelectedChangeFilter);
            Assert.Equal(2, vm.History.Changes.Count);
            Assert.All(vm.History.Changes, c => Assert.True(c.IsWorsened));
            filter.SelectedItem = CheckChangeFilter.Improved;
            Assert.Single(vm.History.Changes);
            filter.SelectedItem = CheckChangeFilter.Changed;
            Render(root, 1080, 740, "history-normal-window");
            Assert.InRange(comparisonPanel.TransformToAncestor(root).Transform(new Point()).Y, 0, 600);
            Render(root, 1580, 1050, "history-wide");
            Render(root, 1580, 1300, "history-full");
            Assert.Equal(0, Grid.GetRow((UIElement)history.FindName("ComparisonPanel")));
            Assert.Equal(0, Grid.GetRow(scanPanel));
            Assert.Equal(355, scanGrid.Height);
            var selector = (ComboBox)history.FindName("BaselineSelector");
            Assert.Contains(Descendants<TextBlock>(selector), t => t.Text == vm.History.Baseline!.Label);
            selector.SelectedItem = vm.History.Scans[3];
            root.UpdateLayout();
            var selectedId = vm.History.Baseline!.Record.Id;
            await vm.History.LoadAsync("model");
            root.UpdateLayout();
            Assert.Equal(selectedId, vm.History.Baseline!.Record.Id);
            Assert.Same(vm.History.Baseline, selector.SelectedItem);
            Render(root, 780, 700, "history-minimum");
            Assert.Equal(1, Grid.GetRow(scanPanel));
            vm.History.Current = vm.History.Baseline;
            filter.SelectedItem = CheckChangeFilter.Worsened;
            Render(root, 1080, 1050, "history-no-worsened");
            Assert.Contains(Descendants<TextBlock>(comparisonPanel), t => t.Text == "No checks worsened between these scans." && t.Visibility == Visibility.Visible && t.ActualHeight > 0);

            foreach (var rate in new[] { 50, 50, 80 })
            {
                service.Append("model", new HealthScanRecord
                {
                    ModelKey = "model", Timestamp = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc).AddMinutes(vm.History.Records.Count),
                    PassRate = rate
                });
                await vm.History.LoadAsync("model");
                Render(root, 1080, 740, "history-tiles-" + vm.History.Records.Count);
                Assert.Equal(vm.History.LatestDelta, badgeText.Text);
                var expectedColor = vm.History.LatestChange > 0 ? Color.FromRgb(0x10, 0xb9, 0x81) : Color.FromRgb(0x9c, 0xa3, 0xaf);
                Assert.Equal(expectedColor, ((SolidColorBrush)badgeText.Foreground).Color);
                Assert.Contains(Descendants<TextBlock>(tiles), t => t.Text == vm.History.BestComparisonCaption);
            }

            await vm.History.LoadAsync("empty");
            Render(root, 1080, 740, "history-empty");
            Assert.False(vm.History.HasHistory);
            Assert.Equal(Visibility.Collapsed, badge.Visibility);
            Assert.Empty(trace.Errors);
        }
        finally
        {
            window?.Close();
            vm.Cleanup();
            PresentationTraceSources.DataBindingSource.Listeners.Remove(trace);
            PresentationTraceSources.DataBindingSource.Switch.Level = oldLevel;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    });

    private static void Render(FrameworkElement root, int width, int height, string name)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        root.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var output = Environment.GetEnvironmentVariable("PULSE_UI_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        using var stream = File.Create(Path.Combine(output, name + ".png"));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private sealed class BindingTrace : TraceListener
    {
        public List<string> Errors { get; } = new();
        public override void Write(string? message) { if (!string.IsNullOrWhiteSpace(message)) Errors.Add(message!); }
        public override void WriteLine(string? message) => Write(message);
    }
}
