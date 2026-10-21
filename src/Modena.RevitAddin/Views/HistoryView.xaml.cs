using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Modena.RevitAddin.Views;

public partial class HistoryView : UserControl
{
    public HistoryView() => InitializeComponent();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Keep tables readable at the existing window's minimum width.
        var stacked = e.NewSize.Width < 1250;
        ScanGrid.Height = stacked ? 240 : 355;
        HistoryChart.Height = stacked ? 180 : 230;
    }
}

public sealed class DeltaBrushConverter : IValueConverter
{
    private static readonly Brush Improved = new SolidColorBrush(Color.FromRgb(0x10, 0xb9, 0x81));
    private static readonly Brush Regressed = new SolidColorBrush(Color.FromRgb(0xf4, 0x3f, 0x5e));
    private static readonly Brush Neutral = new SolidColorBrush(Color.FromRgb(0x9c, 0xa3, 0xaf));

    static DeltaBrushConverter()
    {
        Improved.Freeze();
        Regressed.Freeze();
        Neutral.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int delta && delta != 0 ? delta > 0 ? Improved : Regressed : Neutral;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
