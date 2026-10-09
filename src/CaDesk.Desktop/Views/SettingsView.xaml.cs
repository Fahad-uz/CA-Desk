using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using CaDesk.Desktop.ViewModels;
namespace CaDesk.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();
    private async void SaveProfile(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm) await vm.SaveProfileAsync();
    }
    private async void ExportBackup(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;
        var picker = new Microsoft.Win32.SaveFileDialog { Title = "Export CA Desk backup", Filter = "CA Desk database (*.db)|*.db", FileName = $"CA-Desk-backup-{DateTime.Now:yyyy-MM-dd-HHmmss}.db", AddExtension = true, DefaultExt = ".db" };
        if (picker.ShowDialog(Window.GetWindow(this)) == true) await vm.ExportAsync(picker.FileName);
    }
    private async void RestoreBackup(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Restore CA Desk backup", Filter = "CA Desk database (*.db)|*.db", CheckFileExists = true };
        if (picker.ShowDialog(Window.GetWindow(this)) == true) await vm.RestoreAsync(picker.FileName);
    }
    private void OpenBackups(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;
        try
        {
            System.IO.Directory.CreateDirectory(vm.BackupDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{vm.BackupDirectory}\"") { UseShellExecute = true });
        }
        catch { MessageBox.Show($"The backups folder could not be opened. Its path is {vm.BackupDirectory}", "CA Desk"); }
    }
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
