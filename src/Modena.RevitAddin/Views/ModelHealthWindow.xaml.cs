using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Modena.RevitAddin.Services;
using Modena.RevitAddin.ViewModels;

namespace Modena.RevitAddin.Views;

/// <summary>
/// Code-behind for the Model Health Checker WPF window.
/// </summary>
public partial class ModelHealthWindow : Window
{
    private TextBlock? _accessStatus;
    private Button? _accessButton;

    public ModelHealthWindow(ModelHealthViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        AddAccessBar(viewModel);
        viewModel.OnRefreshCompleted = UpdateAccessBar;

        Closed += OnClosed;
        // On first render: restore cached data (if any) then background-refresh,
        // or do a full interactive load when no cache exists for this model.
        ContentRendered += async (_, _) => await viewModel.InitializeAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is ModelHealthViewModel vm)
        {
            vm.Cleanup();
        }
    }

    private async void OnHistorySelected(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, e.OriginalSource) && DataContext is ModelHealthViewModel vm)
            await vm.ReloadHistoryAsync();
    }

    private void AddAccessBar(ModelHealthViewModel viewModel)
    {
        if (viewModel.AccessPolicy is null || Content is not Grid mainGrid) return;
        var panel = new DockPanel { Margin = new Thickness(12, 6, 12, 6) };
        _accessButton = new Button { Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(12, 0, 0, 0) };
        _accessButton.Click += OnAccessClick;
        DockPanel.SetDock(_accessButton, Dock.Right);
        panel.Children.Add(_accessButton);
        _accessStatus = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = System.Windows.Media.Brushes.White };
        panel.Children.Add(_accessStatus);
        var bar = new Border { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 55, 72)), Child = panel };
        if (mainGrid.RowDefinitions.Count == 0)
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });
        foreach (UIElement child in mainGrid.Children) Grid.SetRow(child, Grid.GetRow(child) + 1);
        Grid.SetColumnSpan(bar, Math.Max(1, mainGrid.ColumnDefinitions.Count));
        mainGrid.Children.Add(bar);
        UpdateAccessBar();
    }

    private void UpdateAccessBar()
    {
        if (_accessStatus is null || _accessButton is null || DataContext is not ModelHealthViewModel vm) return;
        var paid = vm.AccessPolicy?.IsPaidAccessActive == true;
        _accessButton.Content = paid ? "Licence / Profile" : "Sign In / Activate Licence";
        _accessStatus.Text = paid ? "Pulse — MLBS licence active" : "Pulse trial — activate your licence to continue after expiry";
#if TRIAL_VERSION
        if (!paid) _accessStatus.Text = TrialManager.GetTrialStatus().GetStatusMessage();
#endif
    }

    private async void OnAccessClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModelHealthViewModel vm || vm.AccessPolicy is null || _accessButton is null) return;
        _accessButton.IsEnabled = false;
        try
        {
            var wasPaid = vm.AccessPolicy.IsPaidAccessActive;
            if (vm.AccessPolicy.IsPaidAccessActive)
                await vm.AccessPolicy.ShowProfileAsync(this);
            else
            {
                var result = await vm.AccessPolicy.ActivateAsync(this);
                if (!result.IsAllowed) MessageBox.Show(this, result.Message, "Pulse — Activation", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            UpdateAccessBar();
            if (wasPaid != vm.AccessPolicy.IsPaidAccessActive)
            {
                vm.AccessChanged();
                if (IsVisible) await vm.InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Pulse licence dialog failed.", ex);
            MessageBox.Show(this, "Pulse could not verify your licence. Please try again.", "Pulse", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _accessButton.IsEnabled = true; }
    }
}

/// <summary>
/// Converts a bool to inverse Visibility (true=Collapsed, false=Visible).
/// When ConverterParameter is "bool", returns the inverse bool instead.
/// Also converts non-null strings to Visible (for ErrorMessage binding).
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is string p && p == "bool")
        {
            // Return inverse bool for IsEnabled binding
            if (value is bool b) return !b;
            return true;
        }

        // For string values (ErrorMessage): non-null/non-empty = Visible
        if (value is string s)
            return string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;

        if (value is bool boolVal)
            return boolVal ? Visibility.Collapsed : Visibility.Visible;

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
