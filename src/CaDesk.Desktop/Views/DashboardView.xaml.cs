using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace CaDesk.Desktop.Views;

public partial class DashboardView : UserControl
{
    public DashboardView() => InitializeComponent();
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var grid = FindGrid(this);
        if (grid != null)
            grid.Columns = ActualWidth >= 1100 ? 6 : 3;
    }
    private static System.Windows.Controls.Primitives.UniformGrid? FindGrid(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var c = VisualTreeHelper.GetChild(parent, i);
            if (c is System.Windows.Controls.Primitives.UniformGrid g)
                return g;
            var n = FindGrid(c);
            if (n != null)
                return n;
        }
        return null;
    }
}
