using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
namespace CaDesk.Desktop.Controls;

public static class SmoothProgress
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached("Value", typeof(double), typeof(SmoothProgress), new PropertyMetadata(0d, Changed));
    public static void SetValue(DependencyObject target, double value) => target.SetValue(ValueProperty, value);
    public static double GetValue(DependencyObject target) => (double)target.GetValue(ValueProperty);
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not ProgressBar bar)
            return;
        var from = bar.Value;
        var to = (double)e.NewValue;
        bar.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, to);
        if (!SystemParameters.ClientAreaAnimation)
            return;
        bar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
    }
}
