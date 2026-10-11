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
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Capture(window, Path.Combine(folder, "first-launch-caption.png"));
                File.WriteAllText(Path.Combine(folder, "first-caption-bounds.txt"), WindowViewport.CaptionDiagnostics(window));
                if (!WindowViewport.CaptionIsOnScreen(window)) throw new InvalidOperationException("Packaged first-launch caption is outside the usable screen area or cannot receive input: " + WindowViewport.CaptionDiagnostics(window));
                await store.ReloadAsync();
                await store.SaveClientAsync(null, "Portable launch test", "", DateOnly.FromDateTime(DateTime.Today).AddDays(5));
                var c = store.Snapshot.Clients.Single();
                await store.SaveWorkAsync(null, c.Id, "Persistence check", WorkStatus.NotStarted, DateOnly.FromDateTime(DateTime.Today), "Isolated package verification");
                var work = store.Snapshot.WorkAreas.Single();
                await store.UpdateStatusAsync(work.Id, WorkStatus.Completed);
                var reopened = new Repository(path).Load();
                if (reopened.WorkAreas.Single().Status != WorkStatus.Completed || window.Shell.Dashboard.Progress.Percentage != 100)
                    throw new InvalidOperationException("Packaged persistence check failed.");
                var today = DateOnly.FromDateTime(DateTime.Today);
                await store.SaveNoteAsync(null, "Packaged study note", "First line\nSecond line", today);
                window.Shell.Settings.ProfileName = "Alex Shah";
                await window.Shell.Settings.SaveProfileAsync();
                var backup = Path.Combine(folder, "packaged-export.db"); await store.ExportBackupAsync(backup);
                await store.DeleteClientAsync(c.Id); await store.DeleteNoteAsync(store.Snapshot.Notes.Single().Id);
                await store.SavePreferenceAsync("profile.name", "Temporary name");
                await store.RestoreBackupAsync(backup);
                window.Shell.ApplyPreferences(); window.Shell.Refresh();
                var restored = new Repository(path).Load();
                window.Shell.Calendar.Mode = ViewModels.CalendarMode.Day;
                if (restored.Clients.Single().DueDate != today.AddDays(5) || restored.Notes.Single().Body != "First line\nSecond line" || restored.Preferences["profile.name"] != "Alex Shah" || window.Shell.Greeting != "Welcome back, Alex Shah" || ((TextBlock)window.FindName("SidebarProfileName")).Text != "Alex Shah" || window.Shell.Calendar.Days.Single().Items.Count != 2 || window.Shell.Dashboard.Progress.Percentage != 100)
                    throw new InvalidOperationException("Packaged notes, due dates, backup or calendar verification failed.");
                window.Shell.Navigate("Notes");
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
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                if (!WindowViewport.CaptionIsOnScreen(window)) throw new InvalidOperationException("Packaged maximized caption is outside the usable screen area or cannot receive input.");
                Capture(window, Path.Combine(folder, "maximized-caption.png"));
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
                File.WriteAllText(Path.Combine(folder, "result.txt"), "PASS: Published self-contained single-file win-x64 executable launched with every caption button on screen and hit-testable before any minimize/restore, retained visible maximized controls, rendered WPF, wrote SQLite, reopened it, preserved study notes, client due dates and profile name, exported/restored a verified backup, displayed the personal greeting and sidebar name, updated the calendar and dashboard progress and operated Minimize, Maximize, Restore and Close title-bar buttons.");
                app.Shutdown(0);
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(folder, "failure.txt"), e.ToString()); app.Shutdown(1); }
        };
        window.Show();
    }
    private static void Capture(Window window, string path)
    {
        window.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(window);
        var image = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        image.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path); png.Save(stream);
    }
    private static async Task WaitForState(Window window, WindowState state)
    {
        for (var attempt = 0; window.WindowState != state && attempt < 200; attempt++)
            await Task.Delay(25);
        if (window.WindowState != state)
            throw new InvalidOperationException($"Packaged title-bar action did not enter {state} state.");
    }
}
