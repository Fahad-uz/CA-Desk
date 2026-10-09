using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CaDesk.Core;
namespace CaDesk.Desktop.Controls;

public sealed class BooleanVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type target, object parameter, CultureInfo culture) => value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type target, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
public sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type target, object parameter, CultureInfo culture) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(value switch { WorkStatus.Completed => "#16A34A", WorkStatus.InProgress => "#2563EB", _ => "#64748B" }));
    public object ConvertBack(object value, Type target, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
