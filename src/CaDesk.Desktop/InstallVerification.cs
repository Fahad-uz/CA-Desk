using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CaDesk.Core;
namespace CaDesk.Desktop;

internal static class InstallVerification
{
    public static void Start(Application app, string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "install-verification.db");
        var store = new ApplicationStore(new Repository(path));
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var window = new MainWindow(store, false);
        app.MainWindow = window;
        window.Loaded += async (_, _) =>
        {
            try
            {
                await store.ReloadAsync();
                await store.SaveClientAsync(null, "Portable launch test", "");
                var c = store.Snapshot.Clients.Single();
                await store.SaveWorkAsync(null, c.Id, "Persistence check", WorkStatus.NotStarted, DateOnly.FromDateTime(DateTime.Today), "Isolated package verification");
                var work = store.Snapshot.WorkAreas.Single();
                await store.UpdateStatusAsync(work.Id, WorkStatus.Completed);
                var reopened = new Repository(path).Load();
                if (reopened.WorkAreas.Single().Status != WorkStatus.Completed || window.Shell.Dashboard.Progress.Percentage != 100)
                    throw new InvalidOperationException("Packaged persistence check failed.");
                var minimize = (Button)window.FindName("MinimizeButton");
                var maximize = (Button)window.FindName("MaximizeRestoreButton");
                if (!minimize.IsVisible || !maximize.IsVisible || !((Button)window.FindName("CloseButton")).IsVisible)
                    throw new InvalidOperationException("Packaged title-bar controls are not visible.");
                minimize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitForState(window, WindowState.Minimized);
                SystemCommands.RestoreWindow(window);
                await WaitForState(window, WindowState.Normal);
                maximize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitForState(window, WindowState.Maximized);
                maximize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitForState(window, WindowState.Normal);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();
                var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                image.Render(window);
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(image));
                using (var stream = File.Create(Path.Combine(folder, "packaged-app.png")))
                    png.Save(stream);
                var closed = false;
                window.Closed += (_, _) => closed = true;
                ((Button)window.FindName("CloseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                for (var attempt = 0; !closed && attempt < 200; attempt++)
                    await Task.Delay(25);
                if (!closed)
                    throw new InvalidOperationException("Packaged Close button did not close the window.");
                File.WriteAllText(Path.Combine(folder, "result.txt"), "PASS: Published self-contained win-x64 executable launched, rendered WPF, wrote SQLite, reopened it, updated dashboard progress and operated Minimize, Maximize, Restore and Close title-bar buttons.");
                app.Shutdown(0);
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(folder, "failure.txt"), e.ToString()); app.Shutdown(1); }
        };
        window.Show();
    }
    private static async Task WaitForState(Window window, WindowState state)
    {
        for (var attempt = 0; window.WindowState != state && attempt < 200; attempt++)
            await Task.Delay(25);
        if (window.WindowState != state)
            throw new InvalidOperationException($"Packaged title-bar action did not enter {state} state.");
    }
}
