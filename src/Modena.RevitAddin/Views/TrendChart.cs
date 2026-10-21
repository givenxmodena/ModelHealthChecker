using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Modena.Shared.DTOs;

namespace Modena.RevitAddin.Views;

/// <summary>Timestamp-scaled health history; geometry is reduced to pixel-bucket extrema.</summary>
public sealed class TrendChart : FrameworkElement
{
    public static readonly DependencyProperty RecordsProperty = DependencyProperty.Register(
        nameof(Records), typeof(IReadOnlyList<HealthScanRecord>), typeof(TrendChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty BaselineProperty = DependencyProperty.Register(
        nameof(Baseline), typeof(HealthScanRecord), typeof(TrendChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CurrentProperty = DependencyProperty.Register(
        nameof(Current), typeof(HealthScanRecord), typeof(TrendChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<HealthScanRecord>? Records { get => (IReadOnlyList<HealthScanRecord>?)GetValue(RecordsProperty); set => SetValue(RecordsProperty, value); }
    public HealthScanRecord? Baseline { get => (HealthScanRecord?)GetValue(BaselineProperty); set => SetValue(BaselineProperty, value); }
    public HealthScanRecord? Current { get => (HealthScanRecord?)GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }

    private static readonly Brush Blue = Brush("#3b82f6");
    private static readonly Brush Amber = Brush("#f59e0b");
    private static readonly Brush Text = Brush("#9ca3af");
    private static readonly Brush GridLine = Brush("#2d323c");
    private readonly Typeface _font = new("Segoe UI");
    private HealthScanRecord? _hovered;
    private Rect Plot => new(42, 18, Math.Max(1, ActualWidth - 66), Math.Max(1, ActualHeight - 54));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 100 || ActualHeight < 70) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var plot = Plot;
        for (var value = 0; value <= 100; value += 20)
        {
            var y = plot.Bottom - plot.Height * value / 100;
            dc.DrawLine(new Pen(GridLine, 0.5), new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(dc, value + "%", new Point(0, y - 7));
        }
        if (Records is not { Count: > 0 } records)
        {
            DrawText(dc, "Successful scans will appear here.", new Point(plot.Left + 12, plot.Top + plot.Height / 2));
            return;
        }

        var indices = VisibleIndices(records, plot);
        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(Position(records[indices[0]], plot), false, false);
            foreach (var i in indices.Skip(1)) ctx.LineTo(Position(records[i], plot), true, false);
        }
        line.Freeze();
        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            var first = Position(records[indices[0]], plot);
            ctx.BeginFigure(new Point(first.X, plot.Bottom), true, true);
            foreach (var i in indices) ctx.LineTo(Position(records[i], plot), true, false);
            ctx.LineTo(new Point(Position(records[indices[indices.Count - 1]], plot).X, plot.Bottom), true, false);
        }
        area.Freeze();
        dc.DrawGeometry(new LinearGradientBrush(Color.FromArgb(55, 59, 130, 246), Colors.Transparent, 90), null, area);
        dc.DrawGeometry(null, new Pen(Blue, 2), line);
        if (records.Count <= 100)
            foreach (var record in records) dc.DrawEllipse(Blue, null, Position(record, plot), 3, 3);

        var labels = Math.Max(1, (int)(plot.Width / 150));
        var durationTicks = records[records.Count - 1].Timestamp.Ticks - records[0].Timestamp.Ticks;
        for (var i = 0; i <= labels; i++)
        {
            if (durationTicks == 0 && i > 0) break;
            var fraction = durationTicks == 0 ? 0.5 : (double)i / labels;
            var time = new DateTime(records[0].Timestamp.Ticks + (long)(durationTicks * fraction), DateTimeKind.Utc);
            var label = time.ToLocalTime().ToString(durationTicks < TimeSpan.TicksPerDay ? "HH:mm:ss" : "d MMM", CultureInfo.CurrentCulture);
            var x = plot.Left + plot.Width * fraction;
            DrawText(dc, label, new Point(Math.Max(0, Math.Min(ActualWidth - 70, x - 20)), plot.Bottom + 12));
        }
        if (Baseline is not null && Baseline.Id == Current?.Id)
        {
            dc.DrawEllipse(null, new Pen(Amber, 2), Position(Baseline, plot), 9, 9);
            DrawMarker(dc, Current, "B/C", Blue);
        }
        else
        {
            DrawMarker(dc, Baseline, "B", Amber);
            DrawMarker(dc, Current, "C", Blue);
        }
    }

    private Point Position(HealthScanRecord record, Rect plot)
    {
        var records = Records!;
        var duration = (records[records.Count - 1].Timestamp - records[0].Timestamp).TotalSeconds;
        var fraction = duration <= 0 ? 0.5 : (record.Timestamp - records[0].Timestamp).TotalSeconds / duration;
        return new Point(plot.Left + plot.Width * fraction,
            plot.Bottom - plot.Height * Math.Max(0, Math.Min(100, record.PassRate)) / 100);
    }

    private List<int> VisibleIndices(IReadOnlyList<HealthScanRecord> records, Rect plot)
    {
        var result = new List<int>();
        var start = 0;
        while (start < records.Count)
        {
            var pixel = (int)Position(records[start], plot).X;
            var end = start;
            var min = start;
            var max = start;
            while (end + 1 < records.Count && (int)Position(records[end + 1], plot).X == pixel)
            {
                end++;
                if (records[end].PassRate < records[min].PassRate) min = end;
                if (records[end].PassRate > records[max].PassRate) max = end;
            }
            foreach (var i in new[] { start, min, max, end }.Distinct().OrderBy(i => i)) result.Add(i);
            start = end + 1;
        }
        return result;
    }

    private void DrawMarker(DrawingContext dc, HealthScanRecord? record, string label, Brush color)
    {
        if (record is null) return;
        var point = Position(record, Plot);
        dc.DrawEllipse(Brush("#1a1d23"), new Pen(color, 2), point, 6, 6);
        DrawText(dc, label + " " + record.PassRate + "%", new Point(Math.Max(0, Math.Min(ActualWidth - 55, point.X - 15)), point.Y - 25), color);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Records is not { Count: > 0 } records) return;
        var mouse = e.GetPosition(this);
        if (!Plot.Contains(mouse)) { ToolTip = null; _hovered = null; return; }
        var fraction = Math.Max(0, Math.Min(1, (mouse.X - Plot.Left) / Plot.Width));
        var target = records[0].Timestamp.Ticks + fraction * (records[records.Count - 1].Timestamp.Ticks - records[0].Timestamp.Ticks);
        var lo = 0;
        var hi = records.Count - 1;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (records[mid].Timestamp.Ticks < target) lo = mid + 1;
            else hi = mid;
        }
        var index = lo > 0 && Math.Abs(records[lo - 1].Timestamp.Ticks - target) <= Math.Abs(records[lo].Timestamp.Ticks - target) ? lo - 1 : lo;
        var nearest = records[index];
        if (ReferenceEquals(_hovered, nearest)) return;
        _hovered = nearest;
        ToolTip = $"{nearest.Timestamp.ToLocalTime():d MMM yyyy · HH:mm:ss}\nPass rate: {nearest.PassRate}% ({nearest.PassedChecks}/{nearest.TotalChecks})\nWarnings: {nearest.WarningsCount:N0} · Elements: {nearest.ModelElements:N0}";
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = null;
        ToolTip = null;
    }

    private void DrawText(DrawingContext dc, string text, Point position, Brush? brush = null) =>
        dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            _font, 11, brush ?? Text, VisualTreeHelper.GetDpi(this).PixelsPerDip), position);

    private static Brush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
