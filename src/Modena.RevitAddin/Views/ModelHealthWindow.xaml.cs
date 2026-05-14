using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Modena.RevitAddin.ViewModels;

namespace Modena.RevitAddin.Views;

/// <summary>
/// Code-behind for the Model Health Checker WPF window.
/// </summary>
public partial class ModelHealthWindow : Window
{
    public ModelHealthWindow(ModelHealthViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closed += OnClosed;
        // Auto-trigger extraction once the window has rendered its initial state,
        // so the user sees the model name immediately and data loads without a button click.
        ContentRendered += (_, _) => viewModel.LoadCommand.Execute(null);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is ModelHealthViewModel vm)
        {
            vm.Cleanup();
        }
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
