using System.Windows;
namespace CaDesk.Desktop;

public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--verify-install")
        {
            try
            {
                InstallVerification.Start(this, System.IO.Path.GetFullPath(e.Args[1]));
            }
            catch { Shutdown(1); }
            return;
        }
        instance = new Mutex(true, "Local\\CaDesk.SingleUser", out var created);
        if (!created)
        {
            MessageBox.Show("CA Desk is already open. Switch to its window to continue.", "CA Desk", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        try
        {
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
        catch (Exception ex) { MessageBox.Show(ex is Core.ValidationException ? ex.Message : "CA Desk could not open its local database. Check that your Windows user data folder is writable and has free space. Existing data has not been reset.", "CA Desk", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        instance?.Dispose();
        base.OnExit(e);
    }
}
