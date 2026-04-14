using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Modena.Shared.DTOs;

namespace Modena.RevitAddin.Views;

/// <summary>
/// A pure-WPF horizontal bar chart for MetricDto items.
/// Colors bars by name: HIGH=red, MEDIUM=amber, LOW=blue, default=slate.
/// </summary>
public class HorizontalBarChart : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(HorizontalBarChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static readonly Dictionary<string, Color> SeverityColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HIGH"] = Color.FromRgb(0xf4, 0x3f, 0x5e),
        ["MEDIUM"] = Color.FromRgb(0xf5, 0x9e, 0x0b),
        ["LOW"] = Color.FromRgb(0x3b, 0x82, 0xf6)
    };

    private static readonly Color TrackColor = Color.FromRgb(0x1f, 0x29, 0x37);
    private static readonly Color LabelColor = Color.FromRgb(0xFF, 0xFF, 0xFF);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var items = ItemsSource?.Cast<MetricDto>().ToList();
        if (items is null || items.Count == 0) return;

        int maxVal = items.Max(m => m.Count);
        if (maxVal <= 0) maxVal = 1;

        double barHeight = 20;
        double gap = 16;
        double y = 0;
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (var item in items)
        {
            // Track
            dc.DrawRoundedRectangle(new SolidColorBrush(TrackColor), null,
                new Rect(0, y, ActualWidth, barHeight), 0, 4);

            // Bar
            double barWidth = (item.Count / (double)maxVal) * ActualWidth;
            var barColor = SeverityColors.TryGetValue(item.Name, out var c) ? c : Color.FromRgb(0x64, 0x74, 0x8b);
            dc.DrawRoundedRectangle(new SolidColorBrush(barColor), null,
                new Rect(0, y, Math.Max(barWidth, 2), barHeight), 0, 4);

            // Label
            var label = $"{item.Name} ({item.Count})";
            var text = new FormattedText(label, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, 9, new SolidColorBrush(LabelColor), dpi);
            dc.DrawText(text, new Point(ActualWidth - text.Width - 8, y + (barHeight - text.Height) / 2));

            y += barHeight + gap;
        }
    }
}
