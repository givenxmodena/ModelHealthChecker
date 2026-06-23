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
#if TRIAL_VERSION
    private Border? _trialBanner;
#endif

    public ModelHealthWindow(ModelHealthViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

#if TRIAL_VERSION
        AddTrialBanner();
        viewModel.OnRefreshCompleted = RefreshTrialBanner;
#endif

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

#if TRIAL_VERSION
    /// <summary>
    /// Adds trial banner to the main window if in trial mode.
    /// </summary>
    private void AddTrialBanner()
    {
        try
        {
            var trialBanner = WatermarkService.CreateTrialBanner();
            if (trialBanner is null)
                return;

            if (Content is Grid mainGrid)
            {
                if (mainGrid.RowDefinitions.Count == 0)
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                mainGrid.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });

                foreach (UIElement child in mainGrid.Children)
                {
                    var currentRow = Grid.GetRow(child);
                    Grid.SetRow(child, currentRow + 1);
                }

                Grid.SetRow(trialBanner, 0);
                Grid.SetColumnSpan(trialBanner, mainGrid.ColumnDefinitions.Count > 0 ? mainGrid.ColumnDefinitions.Count : 1);
                mainGrid.Children.Add(trialBanner);

                _trialBanner = trialBanner;
                LogService.Info("Trial banner added to main window.");
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to add trial banner.", ex);
        }
    }

    private void RefreshTrialBanner()
    {
        Dispatcher.Invoke(() => WatermarkService.RefreshBanner(_trialBanner));
    }
#endif
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
