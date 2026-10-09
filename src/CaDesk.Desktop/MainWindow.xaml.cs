using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CaDesk.Core;
using CaDesk.Desktop.ViewModels;
using CaDesk.Desktop.Views;
namespace CaDesk.Desktop;

public partial class MainWindow : Window
{
    public ShellViewModel Shell
    {
        get;
    }
    private readonly DispatcherTimer dayTimer;
    private DateTime lastDate = DateTime.Today;
    public MainWindow() : this(new ApplicationStore(new Repository(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CA Desk", "ca-desk.db")))) { }
    public MainWindow(ApplicationStore store, bool loadOnOpen = true)
    {
        InitializeComponent();
        Shell = new(store);
        DataContext = Shell;
        Shell.Dialogs = new DialogService(Shell, this);
        Loaded += async (_, _) => { if (loadOnOpen) { Shell.IsBusy = true; await Shell.RunAsync(() => store.ReloadAsync()); Shell.IsBusy = false; } else Shell.Refresh(); };
        dayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        dayTimer.Tick += (_, _) => { if (DateTime.Today != lastDate) { lastDate = DateTime.Today; Shell.Refresh(); } };
        dayTimer.Start();
        Closed += (_, _) => dayTimer.Stop();
        StateChanged += (_, _) => UpdateCaption();
        PreviewKeyDown += OnShortcut;
    }
    private void MinimizeWindow(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void ToggleMaximizeWindow(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(this);
        else
            SystemCommands.MaximizeWindow(this);
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);
    private void UpdateCaption()
    {
        var maximized = WindowState == WindowState.Maximized;
        MaximizeRestoreIcon.Text = maximized ? "\uE923" : "\uE922";
        MaximizeRestoreButton.ToolTip = maximized ? "Restore" : "Maximize";
        System.Windows.Automation.AutomationProperties.SetName(MaximizeRestoreButton, maximized ? "Restore" : "Maximize");
    }
    private void OnShortcut(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            UpdateLayout();
            var search = FindSearch(PageHost);
            if (search != null)
            {
                search.Focus();
                search.SelectAll();
                e.Handled = true;
            }
        }
    }
    private static TextBox? FindSearch(DependencyObject parent)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var c = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (c is TextBox box && System.Windows.Automation.AutomationProperties.GetName(box).StartsWith("Search"))
                return box;
            var result = FindSearch(c);
            if (result != null)
                return result;
        }
        return null;
    }
}
