using System.Globalization;
using System.Windows.Data;

namespace Modena.RevitAddin.Views;

/// <summary>
/// Converts a numeric value to a proportional width for bar charts.
/// Scales value * 2, capped at 300px.
/// </summary>
public class ValueToWidthConverter : IValueConverter
{
    public static readonly ValueToWidthConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intVal)
            return Math.Min(intVal * 2.0, 300.0);
        if (value is double dblVal)
            return Math.Min(dblVal * 2.0, 300.0);
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
