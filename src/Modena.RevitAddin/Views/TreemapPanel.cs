using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Modena.Shared.DTOs;

namespace Modena.RevitAddin.Views;

/// <summary>
/// A simple treemap control that renders CategoryDto items as proportionally-sized colored blocks.
/// Uses a squarified strip layout for a clean visual.
/// </summary>
public class TreemapPanel : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(TreemapPanel),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static readonly Color[] Palette = new[]
    {
        Color.FromArgb(0x99, 0x1e, 0x3a, 0x8a), // blue-900
        Color.FromArgb(0x99, 0x16, 0x4e, 0x63), // cyan-900
        Color.FromArgb(0x99, 0x31, 0x2e, 0x81), // indigo-900
        Color.FromArgb(0x99, 0x4c, 0x1d, 0x95), // purple-900
        Color.FromArgb(0x99, 0x1e, 0x29, 0x3b), // slate-800
        Color.FromArgb(0x99, 0x0f, 0x76, 0x6e), // teal-800
        Color.FromArgb(0x99, 0x3f, 0x3f, 0x46), // zinc-700
        Color.FromArgb(0x99, 0x1c, 0x1c, 0x22), // zinc-900
    };

    private static readonly Color BorderColor = Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var items = ItemsSource?.Cast<CategoryDto>().Where(c => c.Value > 0).OrderByDescending(c => c.Value).ToList();
        if (items is null || items.Count == 0) return;

        var totalValue = items.Sum(c => (double)c.Value);
        if (totalValue <= 0) return;

        var rects = LayoutStrip(items, totalValue, new Rect(0, 0, ActualWidth, ActualHeight));

        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
        var borderPen = new Pen(new SolidColorBrush(BorderColor), 1);

        for (int i = 0; i < rects.Count; i++)
        {
            var rect = rects[i];
            var color = Palette[i % Palette.Length];
            var brush = new SolidColorBrush(color);

            dc.DrawRoundedRectangle(brush, borderPen, rect, 4, 4);

            // Draw label if block is large enough
            if (rect.Width > 50 && rect.Height > 24)
            {
                var label = $"{items[i].Name} ({FormatValue(items[i].Value)})";
                var text = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, 11,
                    new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                text.MaxTextWidth = rect.Width - 8;
                text.MaxTextHeight = rect.Height - 4;
                text.Trimming = TextTrimming.CharacterEllipsis;

                dc.DrawText(text, new Point(rect.X + 6, rect.Y + (rect.Height - text.Height) / 2));
            }
        }
    }

    /// <summary>
    /// Simple strip-based treemap layout: alternates horizontal/vertical strips.
    /// </summary>
    private static List<Rect> LayoutStrip(List<CategoryDto> items, double totalValue, Rect bounds)
    {
        var rects = new List<Rect>();
        double x = bounds.X, y = bounds.Y;
        double remainingWidth = bounds.Width;
        double remainingHeight = bounds.Height;
        bool horizontal = bounds.Width >= bounds.Height;
        double remainingValue = totalValue;

        foreach (var item in items)
        {
            double fraction = item.Value / remainingValue;

            Rect r;
            if (horizontal)
            {
                double w = remainingWidth * fraction;
                r = new Rect(x, y, Math.Max(w - 2, 1), remainingHeight);
                x += w;
                remainingWidth -= w;
            }
            else
            {
                double h = remainingHeight * fraction;
                r = new Rect(x, y, remainingWidth, Math.Max(h - 2, 1));
                y += h;
                remainingHeight -= h;
            }

            remainingValue -= item.Value;
            rects.Add(r);

            // Alternate direction every 2 items for a more treemap-like look
            if (rects.Count % 2 == 0)
                horizontal = !horizontal;
        }

        return rects;
    }

    private static string FormatValue(int value) =>
        value >= 1000 ? $"{value / 1000.0:F1}k" : value.ToString();
}
