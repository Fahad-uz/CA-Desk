using System.Windows;
using System.Windows.Media;
namespace CaDesk.Desktop.Controls;

public sealed class DonutChart : FrameworkElement
{
    public static readonly DependencyProperty NotStartedProperty = DependencyProperty.Register(nameof(NotStarted), typeof(int), typeof(DonutChart), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty InProgressProperty = DependencyProperty.Register(nameof(InProgress), typeof(int), typeof(DonutChart), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CompletedProperty = DependencyProperty.Register(nameof(Completed), typeof(int), typeof(DonutChart), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
    public int NotStarted
    {
        get => (int)GetValue(NotStartedProperty); set => SetValue(NotStartedProperty, value);
    }
    public int InProgress
    {
        get => (int)GetValue(InProgressProperty); set => SetValue(InProgressProperty, value);
    }
    public int Completed
    {
        get => (int)GetValue(CompletedProperty); set => SetValue(CompletedProperty, value);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - 15);
        const double width = 20;
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 240)), width), center, radius, radius);
        var total = NotStarted + InProgress + Completed;
        if (total == 0)
            return;
        double angle = -90;
        foreach (var (count, color) in new[] { (NotStarted, "#94A3B8"), (InProgress, "#2563EB"), (Completed, "#16A34A") })
        {
            var sweep = 360d * count / total;
            if (count == 0)
                continue;
            var pen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)), width);
            if (count == total)
            {
                dc.DrawEllipse(null, pen, center, radius, radius);
                return;
            }
            var gap = Math.Min(2, sweep / 4);
            var start = angle + gap / 2;
            var end = angle + sweep - gap / 2;
            Point P(double degrees) => new(center.X + radius * Math.Cos(degrees * Math.PI / 180), center.Y + radius * Math.Sin(degrees * Math.PI / 180));
            var g = new StreamGeometry();
            using (var context = g.Open())
            {
                context.BeginFigure(P(start), false, false);
                context.ArcTo(P(end), new Size(radius, radius), 0, end - start > 180, SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            dc.DrawGeometry(null, pen, g);
            angle += sweep;
        }
    }
}
