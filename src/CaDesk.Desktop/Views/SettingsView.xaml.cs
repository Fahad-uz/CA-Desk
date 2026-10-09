using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using CaDesk.Desktop.ViewModels;
namespace CaDesk.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();
    private void OpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is SettingsViewModel vm)
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{System.IO.Path.GetDirectoryName(vm.DatabasePath)}\"") { UseShellExecute = true });
        }
        catch { MessageBox.Show("The data folder could not be opened. Its path is shown above.", "CA Desk", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
}
