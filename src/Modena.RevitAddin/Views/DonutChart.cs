using System.Windows;
using System.Windows.Media;

namespace Modena.RevitAddin.Views;

/// <summary>
/// A pure-WPF donut chart that renders three segments: Passed (green), Failed (red), Reports (amber).
/// Bind Passed, Failed, and Reports dependency properties to integer values.
/// </summary>
public class DonutChart : FrameworkElement
{
    public static readonly DependencyProperty PassedProperty =
        DependencyProperty.Register(nameof(Passed), typeof(int), typeof(DonutChart),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FailedProperty =
        DependencyProperty.Register(nameof(Failed), typeof(int), typeof(DonutChart),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ReportsProperty =
        DependencyProperty.Register(nameof(Reports), typeof(int), typeof(DonutChart),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public int Passed { get => (int)GetValue(PassedProperty); set => SetValue(PassedProperty, value); }
    public int Failed { get => (int)GetValue(FailedProperty); set => SetValue(FailedProperty, value); }
    public int Reports { get => (int)GetValue(ReportsProperty); set => SetValue(ReportsProperty, value); }

    private static readonly Color GreenColor = Color.FromRgb(0x10, 0xb9, 0x81);
    private static readonly Color RedColor = Color.FromRgb(0xf4, 0x3f, 0x5e);
    private static readonly Color AmberColor = Color.FromRgb(0xf5, 0x9e, 0x0b);
    private static readonly Color TrackColor = Color.FromRgb(0x1f, 0x29, 0x37);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        double cx = ActualWidth / 2, cy = ActualHeight / 2;
        double outerR = size / 2 - 4;
        double innerR = outerR * 0.65;
        double total = Passed + Failed + Reports;

        // Draw track ring
        var trackPen = new Pen(new SolidColorBrush(TrackColor), outerR - innerR);
        double midR = (outerR + innerR) / 2;
        dc.DrawEllipse(null, trackPen, new Point(cx, cy), midR, midR);

        if (total <= 0) return;

        // Draw segments
        double startAngle = -90; // start from top
        DrawArc(dc, cx, cy, outerR, innerR, startAngle, (Passed / total) * 360, GreenColor);
        startAngle += (Passed / total) * 360;
        DrawArc(dc, cx, cy, outerR, innerR, startAngle, (Failed / total) * 360, RedColor);
        startAngle += (Failed / total) * 360;
        DrawArc(dc, cx, cy, outerR, innerR, startAngle, (Reports / total) * 360, AmberColor);
    }

    private static void DrawArc(DrawingContext dc, double cx, double cy,
        double outerR, double innerR, double startAngleDeg, double sweepDeg, Color color)
    {
        if (sweepDeg < 0.5) return;

        // Clamp to avoid full-circle arc issues
        bool isLargeArc = sweepDeg > 180;
        double startRad = startAngleDeg * Math.PI / 180;
        double endRad = (startAngleDeg + sweepDeg) * Math.PI / 180;

        var outerStart = new Point(cx + outerR * Math.Cos(startRad), cy + outerR * Math.Sin(startRad));
        var outerEnd = new Point(cx + outerR * Math.Cos(endRad), cy + outerR * Math.Sin(endRad));
        var innerEnd = new Point(cx + innerR * Math.Cos(endRad), cy + innerR * Math.Sin(endRad));
        var innerStart = new Point(cx + innerR * Math.Cos(startRad), cy + innerR * Math.Sin(startRad));

        var outerSize = new Size(outerR, outerR);
        var innerSize = new Size(innerR, innerR);

        var fig = new PathFigure { StartPoint = outerStart, IsClosed = true, IsFilled = true };
        fig.Segments.Add(new ArcSegment(outerEnd, outerSize, 0, isLargeArc, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(innerEnd, true));
        fig.Segments.Add(new ArcSegment(innerStart, innerSize, 0, isLargeArc, SweepDirection.Counterclockwise, true));

        var geo = new PathGeometry();
        geo.Figures.Add(fig);

        dc.DrawGeometry(new SolidColorBrush(color), null, geo);
    }
}
