using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CaDesk.Core;
namespace CaDesk.Desktop;
internal static class InstallVerification
{
    public static void Start(Application app,string folder)
    {
        Directory.CreateDirectory(folder);var path=Path.Combine(folder,"install-verification.db");var store=new ApplicationStore(new Repository(path));var window=new MainWindow(store,false);app.MainWindow=window;
        window.Loaded+=async(_,_)=>
        {
            try
            {
                await store.ReloadAsync();await store.SaveClientAsync(null,"Portable launch test","");var c=store.Snapshot.Clients.Single();
                await store.SaveWorkAsync(null,c.Id,"Persistence check",WorkStatus.NotStarted,DateOnly.FromDateTime(DateTime.Today),"Isolated package verification");
                var work=store.Snapshot.WorkAreas.Single();await store.UpdateStatusAsync(work.Id,WorkStatus.Completed);var reopened=new Repository(path).Load();
                if(reopened.WorkAreas.Single().Status!=WorkStatus.Completed||window.Shell.Dashboard.Progress.Percentage!=100)throw new InvalidOperationException("Packaged persistence check failed.");
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);window.UpdateLayout();
                var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));
                using(var stream=File.Create(Path.Combine(folder,"packaged-app.png")))png.Save(stream);
                File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: Published self-contained win-x64 executable launched, rendered WPF, wrote SQLite, reopened it and updated dashboard progress.");app.Shutdown(0);
            }
            catch(Exception e){File.WriteAllText(Path.Combine(folder,"failure.txt"),e.ToString());app.Shutdown(1);}
        };
        window.Show();
    }
}
