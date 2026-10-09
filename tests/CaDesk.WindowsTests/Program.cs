using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Data.Sqlite;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CaDesk.Core;
using CalendarMode = CaDesk.Desktop.ViewModels.CalendarMode;
using CaDesk.Desktop;
using CaDesk.Desktop.ViewModels;
using CaDesk.Desktop.Views;
namespace CaDesk.WindowsTests;

internal static class Program
{
    private static readonly List<string> passed = [];
    private static readonly List<string> bindingErrors = [];
    private static string output = "";
    [STAThread]
    private static int Main(string[] args)
    {
        output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/windows-checks");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/CA-Desk;component/Themes/Design.xaml") });
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        PresentationTraceSources.DataBindingSource.Listeners.Add(new CaptureTrace());
        var result = 1;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
        {
            try
            {
                if (args.Length > 1) await UpgradeVerification.Run(Path.GetFullPath(args[1]), Check);
                else await Run();
                if (bindingErrors.Count > 0)
                    throw new Exception("WPF binding errors: " + string.Join("\n", bindingErrors));
                Check(true, "No WPF binding errors while rendering all pages and forms");
                result = 0;
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(output, "failure.txt"), e.ToString()); Console.Error.WriteLine(e); }
            finally { File.WriteAllLines(Path.Combine(output, "results.txt"), passed); Console.WriteLine($"Windows WPF checks passed: {passed.Count}"); app.Shutdown(); Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
        });
        Dispatcher.Run();
        return result;
    }
    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        passed.Add("PASS: " + name);
        Console.WriteLine("PASS: " + name);
    }
    private static async Task Until(Func<bool> ready)
    {
        var timer = Stopwatch.StartNew();
        while (!ready())
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(15))
                throw new TimeoutException("Waiting for UI save/update timed out.");
            await Task.Delay(30);
        }
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }
    private static IEnumerable<T> Children<T>(DependencyObject p) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(p); i++)
        {
            var c = VisualTreeHelper.GetChild(p, i);
            if (c is T value)
                yield return value;
            foreach (var n in Children<T>(c))
                yield return n;
        }
    }
    private static async Task Capture(Window window, string name, int? requestedWidth = null, int? requestedHeight = null)
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        var root = (FrameworkElement)window.Content;
        var width = requestedWidth ?? (window is MainWindow ? 1420 : 580);
        var height = requestedHeight ?? (window is MainWindow ? 900 : 680);
        var surface = new Grid { Width = width, Height = height, Background = window.Background, DataContext = window.DataContext };
        surface.Resources.MergedDictionaries.Add(window.Resources);
        window.Content = null;
        surface.Children.Add(root);
        try
        {
            surface.Measure(new Size(width, height));
            surface.Arrange(new Rect(0, 0, width, height));
            surface.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            surface.Measure(new Size(width, height));
            surface.Arrange(new Rect(0, 0, width, height));
            surface.UpdateLayout();
            if (Math.Abs(surface.ActualWidth - width) > 0.5 || Math.Abs(surface.ActualHeight - height) > 0.5)
                throw new Exception($"Requested {width}x{height}, rendered {surface.ActualWidth}x{surface.ActualHeight}.");
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(surface);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png"));
            encoder.Save(stream);
        }
        finally
        {
            surface.Children.Remove(root);
            surface.Resources.MergedDictionaries.Remove(window.Resources);
            window.Content = root;
        }
    }
    private static async Task Run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "CaDeskWindowsTests", Guid.NewGuid().ToString("N"));
        var repo = new Repository(Path.Combine(folder, "checks.db"));
        var store = new ApplicationStore(repo);
        await store.ReloadAsync();
        var window = new MainWindow(store, false);
        window.Show();
        await Until(() => window.IsLoaded);
        var minimize = (Button)window.FindName("MinimizeButton");
        var maximize = (Button)window.FindName("MaximizeRestoreButton");
        var close = (Button)window.FindName("CloseButton");
        var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window);
        Check(minimize.IsVisible && maximize.IsVisible && close.IsVisible && chrome.CaptionHeight > 0 && chrome.ResizeBorderThickness.Left > 0 && System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(close), "Visible title-bar controls retain native caption and resizing behavior");
        minimize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(() => window.WindowState == WindowState.Minimized);
        Check(window.IsVisible, "Minimize title-bar button minimizes without closing the app");
        SystemCommands.RestoreWindow(window);
        await Until(() => window.WindowState == WindowState.Normal);
        maximize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(() => window.WindowState == WindowState.Maximized);
        Check(System.Windows.Automation.AutomationProperties.GetName(maximize) == "Restore", "Maximize title-bar button maximizes and changes to Restore");
        maximize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(() => window.WindowState == WindowState.Normal);
        Check(System.Windows.Automation.AutomationProperties.GetName(maximize) == "Maximize", "Restore title-bar button returns to normal window size");
        var shell = window.Shell;
        var dialogs = new TestDialogs();
        shell.Dialogs = dialogs;
        await Capture(window, "00-empty-dashboard");
        Check(shell.Dashboard.Cards.All(c => c.Count == 0), "Empty first launch has no fabricated statistics");
        var ce = new ClientEditorViewModel(shell, null) { Name = "Acme Private Limited", Description = "Fictional acceptance-test client" };
        ce.SaveCommand.Execute(null);
        await Until(() => store.Snapshot.Clients.Count == 1);
        var cid = store.Snapshot.Clients[0].Id;
        Check(cid > 0, "Create a client through the editor save command");
        var renamed = new ClientEditorViewModel(shell, store.Snapshot.Clients[0]) { Name = "Acme Limited" };
        renamed.SaveCommand.Execute(null);
        await Until(() => store.Snapshot.Clients[0].Name == "Acme Limited");
        Check(true, "Rename a client and update the shared snapshot");
        var duplicate = new ClientEditorViewModel(shell, null) { Name = " acme limited " };
        duplicate.SaveCommand.Execute(null);
        await Until(() => duplicate.HasError);
        Check(duplicate.Error.Contains("already exists") && store.Snapshot.Clients.Count == 1, "Duplicate client form displays a friendly warning without adding data");
        await store.SaveClientAsync(null, "Beacon Traders", "");
        await store.SaveClientAsync(null, "Cedar Partners", "");
        shell.Navigate("Clients");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        var clientSearch = Children<TextBox>(window).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Search clients");
        clientSearch.Text = "ACM";
        await Until(() => shell.Clients.Items.Count == 1);
        Check(shell.Clients.Items[0].Name == "Acme Limited", "Client search control is partial and case-insensitive");
        shell.Clients.Search = "";
        var today = DateOnly.FromDateTime(DateTime.Today);
        for (var i = 0; i < 5; i++)
        {
            var editor = new WorkEditorViewModel(shell, null, cid) { Name = i == 0 ? "GST Filing" : "Work area " + i, Notes = i == 0 ? "Waiting for bank statements.\nFollow up with the client." : "", Deadline = today.ToDateTime(TimeOnly.MinValue) };
            editor.SaveCommand.Execute(null);
            var expected = i + 1;
            await Until(() => store.Snapshot.WorkAreas.Count == expected);
        }
        Check(store.Snapshot.WorkAreas.All(w => w.Status == WorkStatus.NotStarted), "Multiple custom work areas default to Not Started");
        var invalidEditor = new WorkEditorViewModel(shell, store.Snapshot.Rows[0], null);
        var validationWindow = new EditorWindow(invalidEditor) { Owner = window };
        validationWindow.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        validationWindow.UpdateLayout();
        var dateInput = Children<DatePickerTextBox>(validationWindow).Single();
        dateInput.Text = "invalid date";
        invalidEditor.SaveCommand.Execute(null);
        await Until(() => invalidEditor.HasError);
        Check(invalidEditor.Error.Contains("valid date"), "Invalid typed dates block form saving with a helpful message");
        dateInput.Text = today.ToDateTime(TimeOnly.MinValue).ToShortDateString();
        Check(invalidEditor.DeadlineInputValid, "Correcting a typed date clears its validation state");
        validationWindow.Close();
        var first = store.Snapshot.WorkAreas.First();
        var row = store.Snapshot.Rows.First(w => w.Id == first.Id);
        var edit = new WorkEditorViewModel(shell, row, null) { Name = "GST Filing — review", Notes = "Updated notes\nSecond line", Deadline = today.AddDays(-1).ToDateTime(TimeOnly.MinValue) };
        edit.SaveCommand.Execute(null);
        await Until(() => store.Snapshot.WorkAreas.First(w => w.Id == first.Id).Name == edit.Name);
        Check(store.Snapshot.WorkAreas.First(w => w.Id == first.Id).Notes.Contains('\n'), "Work details save name, deadline and multiline notes");
        shell.Navigate("Master Work Table");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        var combo = Children<ComboBox>(window).First(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Work status");
        combo.IsDropDownOpen = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((Popup)combo.Template.FindName("PART_Popup", combo)).IsOpen, "Styled status dropdown opens its native selection popup");
        combo.SelectedValue = WorkStatus.InProgress;
        combo.IsDropDownOpen = false;
        await Until(() => store.Snapshot.WorkAreas.First(w => w.Id == first.Id).Status == WorkStatus.InProgress);
        Check(shell.Dashboard.Progress.InProgress == 1 && shell.Master.Items.First(w => w.Id == first.Id).IsOverdue, "Inline status dropdown persists and updates dashboard while overdue stays separate");
        var datePicker = Children<DatePicker>(window).First(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Work deadline");
        datePicker.IsDropDownOpen = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var datePopup = (Popup)datePicker.Template.FindName("PART_Popup", datePicker);
        Check(datePopup.IsOpen && datePopup.Child is System.Windows.Controls.Calendar, "Styled date picker opens the native calendar");
        ((System.Windows.Controls.Calendar)datePopup.Child).SelectedDate = today.AddDays(8).ToDateTime(TimeOnly.MinValue);
        datePicker.IsDropDownOpen = false;
        await Until(() => store.Snapshot.WorkAreas.First(w => w.Id == first.Id).Deadline == today.AddDays(8));
        Check(true, "Inline native date picker edits the deadline");
        shell.Master.Items.First(w => w.Id == first.Id).ClearDeadlineCommand.Execute(null);
        await Until(() => store.Snapshot.WorkAreas.First(w => w.Id == first.Id).Deadline == null);
        Check(store.Snapshot.WorkAreas.First(w => w.Id == first.Id).Notes == "Updated notes\nSecond line", "Remove deadline preserves notes and status");
        await store.UpdateDeadlineAsync(first.Id, today);
        shell.Master.Search = "GST";
        shell.Master.StatusFilter = WorkStatus.InProgress;
        shell.Master.DeadlineFilter = DeadlineFilter.Today;
        shell.Master.ClientFilter = cid;
        Check(shell.Master.Items.Count == 1, "Master search combines with status, deadline and client filters");
        await store.UpdateStatusAsync(first.Id, WorkStatus.Completed);
        Check(shell.Master.Items.Count == 0 && shell.Master.Search == "GST" && shell.Master.StatusFilter == WorkStatus.InProgress, "Active master filters are preserved after saving an edit");
        shell.Master.ResetCommand.Execute(null);
        Check(shell.Master.Items.Count == 5, "Reset Filters restores all work areas");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var statusFilterControl = Children<ComboBox>(window).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Filter by status");
        Check(statusFilterControl.SelectedItem is StatusFilterOption { Value: null }, "Reset shows the All Statuses selection clearly");
        foreach (var sort in Enum.GetValues<WorkSort>())
        {
            shell.Master.SortBy(sort);
            shell.Master.SortBy(sort);
        }
        Check(shell.Master.Items.Count == 5, "Every master header sort works in both directions");
        shell.Master.Search = "nothing matches";
        Check(shell.Master.IsEmpty, "No matching work displays the empty state");
        shell.Master.Search = "";
        shell.Navigate("Calendar");
        shell.Calendar.Mode = CalendarMode.Month;
        var day = shell.Calendar.Days.Single(d => d.Date == today);
        Check(shell.Calendar.Days.Count == 42 && day.ExtraCount == 2, "Month calendar maps deadlines and shows overflow");
        day.OpenDayCommand.Execute(null);
        Check(shell.Calendar.Mode == CalendarMode.Day && shell.Calendar.Days.Single().Items.Count == 5, "Calendar overflow opens the full day list");
        shell.Calendar.Days[0].Items[0].OpenCommand.Execute(null);
        Check(dialogs.LastWork != null, "Calendar entry opens its actual work details");
        var calendarEditor = new WorkEditorViewModel(shell, dialogs.LastWork, null) { Status = WorkStatus.Completed, Notes = "Updated from calendar" };
        calendarEditor.SaveCommand.Execute(null);
        await Until(() => store.Snapshot.WorkAreas.First(w => w.Id == dialogs.LastWork!.Id).Notes == "Updated from calendar");
        Check(shell.Dashboard.Progress.Completed >= 1, "Editing status and notes from calendar updates shared dashboard progress");
        shell.Calendar.Mode = CalendarMode.Week;
        Check(shell.Calendar.Days.Count == 7, "Week view shows a full Monday–Sunday week");
        shell.Navigate("Dashboard");
        shell.Navigate("Calendar");
        Check(shell.Calendar.Mode == CalendarMode.Week, "Calendar view is remembered during navigation");
        shell.Calendar.ClientFilter = store.Snapshot.Clients.First(c => c.Id != cid).Id;
        Check(shell.Calendar.Days.All(d => d.Items.Count == 0), "Calendar client filter excludes other clients");
        shell.Calendar.ClientFilter = null;
        shell.Calendar.TodayCommand.Execute(null);
        shell.Dashboard.Cards.Single(c => c.Label == "Completed").Command.Execute(null);
        Check(shell.CurrentPage == shell.Master && shell.Master.StatusFilter == WorkStatus.Completed, "Dashboard summary cards navigate with the correct table filter");
        shell.Master.ResetCommand.Execute(null);
        shell.OpenClient(cid);
        Check(((WorkTableViewModel)shell.CurrentPage).Progress.Percentage == Progress.From(store.Snapshot.WorkAreas.Where(w => w.ClientId == cid)).Percentage, "Client workspace progress is calculated from actual completed work");
        var emptyClient = store.Snapshot.Clients.First(c => c.Id != cid);
        shell.OpenClient(emptyClient.Id);
        Check(((WorkTableViewModel)shell.CurrentPage).Progress.Label == "No work areas", "Client with zero work areas has a helpful progress label");
        dialogs.AllowDelete = false;
        await shell.DeleteWorkAsync(store.Snapshot.Rows[0]);
        Check(store.Snapshot.WorkAreas.Count == 5, "Canceling delete preserves the work area");
        dialogs.AllowDelete = true;
        await store.UpdateStatusAsync(first.Id, WorkStatus.NotStarted);
        await store.UpdateDeadlineAsync(first.Id, today.AddDays(-2));
        Check(shell.Dashboard.Cards.Single(c => c.Label == "Overdue Work").Count == 1, "Dashboard overdue count excludes completed and future work");
        await RunStudyNotes(window, store, dialogs, cid, today, folder);
        shell.Navigate("Dashboard");
        window.Width = 1920;
        window.Height = 1080;
        await Capture(window, "01-dashboard-1920", 1920, 1080);
        window.Width = 1420;
        window.Height = 900;
        foreach (var page in new[] { "Dashboard", "Clients", "Master Work Table", "Settings", "Notes" })
        {
            shell.Navigate(page);
            await Capture(window, "02-" + page.Replace(' ', '-'));
        }
        shell.Navigate("Clients");
        shell.Clients.TableView = true;
        await Capture(window, "03-clients-table");
        shell.Clients.TableView = false;
        shell.OpenClient(cid);
        await Capture(window, "04-client-workspace");
        shell.Navigate("Calendar");
        foreach (var mode in Enum.GetValues<CalendarMode>())
        {
            shell.Calendar.Mode = mode;
            await Capture(window, "05-calendar-" + mode);
        }
        var form = new EditorWindow(new WorkEditorViewModel(shell, store.Snapshot.Rows[0], null)) { Owner = window };
        form.Show();
        await Capture(form, "06-work-details");
        form.Close();
        var clientForm = new EditorWindow(new ClientEditorViewModel(shell, store.Snapshot.Clients[0])) { Owner = window };
        clientForm.Show();
        await Capture(clientForm, "07-client-details");
        clientForm.Close();
        window.Width = 1000;
        window.Height = 680;
        foreach (var page in new[] { "Dashboard", "Master Work Table", "Calendar", "Clients", "Settings", "Notes" })
        {
            shell.Navigate(page);
            await Capture(window, "08-small-" + page.Replace(' ', '-'), 1000, 680);
        }
        Check(true, "All pages and edit forms render at desktop and smaller laptop sizes");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = repo.DatabasePath, ForeignKeys = true, Pooling = false }.ToString()))
        {
            db.Open();
            using var tx = db.BeginTransaction();
            using var cmd = db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO work_areas(client_id,name,status,notes,created_at,updated_at) VALUES($client,$name,0,'',$now,$now)";
            cmd.Parameters.AddWithValue("$client", cid);
            var param = cmd.Parameters.Add("$name", SqliteType.Text);
            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            for (var i = 0; i < 1500; i++)
            {
                param.Value = "Volume check " + i;
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        await store.ReloadAsync();
        shell.Navigate("Master Work Table");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        var largeGrid = Children<DataGrid>(window).Single();
        largeGrid.ScrollIntoView(shell.Master.Items[^1]);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        Check(shell.Master.Items.Count == 1505 && Children<DataGridRow>(largeGrid).Count() < 100, "Master table virtualises rows and scrolls through 1,505 work areas");
        var keep = store.Snapshot.WorkAreas.Count;
        var closedByCaption = false;
        window.Closed += (_, _) => closedByCaption = true;
        ((Button)window.FindName("CloseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(() => closedByCaption);
        Check(!window.IsVisible, "Close title-bar button closes the app window after saved work");
        var noteCount = store.Snapshot.Notes.Count;
        var reopened = new ApplicationStore(new Repository(repo.DatabasePath));
        await reopened.ReloadAsync();
        var window2 = new MainWindow(reopened, false);
        window2.Show();
        await Capture(window2, "09-reopened");
        Check(reopened.Snapshot.WorkAreas.Count == keep && reopened.Snapshot.WorkAreas.First(w => w.Id == first.Id).Notes.Length > 0, "Closing and reopening the WPF app preserves saved SQLite data");
        Check(reopened.Snapshot.Notes.Count == noteCount && reopened.Snapshot.Clients.Single(c => c.Id == cid).DueDate == today.AddDays(3), "Study notes and client due dates survive closing and reopening the actual Windows interface");
        window2.Shell.Dialogs = dialogs;
        await window2.Shell.DeleteWorkAsync(reopened.Snapshot.Rows.First(w => w.Id == first.Id));
        Check(reopened.Snapshot.WorkAreas.Count == keep - 1, "Confirmed work deletion updates every connected view");
        await window2.Shell.DeleteClientAsync(reopened.Snapshot.Clients.First(c => c.Id == cid));
        Check(!reopened.Snapshot.WorkAreas.Any(w => w.ClientId == cid) && reopened.Snapshot.Clients.Count == 2, "Confirmed client deletion cascades its work and retains other clients");
        window2.Close();
        Directory.Delete(folder, true);
    }
    private static async Task RunStudyNotes(MainWindow window, ApplicationStore store, TestDialogs dialogs, long cid, DateOnly today, string folder)
    {
        var shell = window.Shell;
        var client = store.Snapshot.Clients.Single(c => c.Id == cid);
        var due = new ClientEditorViewModel(shell, client) { DueDate = today.AddDays(3).ToDateTime(TimeOnly.MinValue) };
        due.SaveCommand.Execute(null);
        await Until(() => store.Snapshot.Clients.Single(c => c.Id == cid).DueDate == today.AddDays(3));
        var card = shell.Clients.Items.Single(c => c.Id == cid);
        Check(card.DaysRemainingLabel == "3 days remaining", "Client editor saves a date and client cards display remaining calendar days");
        shell.OpenClient(cid);
        Check(((WorkTableViewModel)shell.CurrentPage).ClientDueLabel.Contains("3 days remaining"), "Client workspace shows its due date and remaining days");
        var dueForm = new EditorWindow(new ClientEditorViewModel(shell, store.Snapshot.Clients.Single(c => c.Id == cid))) { Owner = window }; dueForm.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); dueForm.UpdateLayout();
        Children<DatePickerTextBox>(dueForm).Single().Text = "invalid";
        var dueVm = (ClientEditorViewModel)dueForm.DataContext; dueVm.SaveCommand.Execute(null); await Until(() => dueVm.HasError);
        Check(store.Snapshot.Clients.Single(c => c.Id == cid).DueDate == today.AddDays(3), "Invalid client due date cannot overwrite its saved date"); dueForm.Close();
        shell.Navigate("Notes");
        Check(((Button)window.FindName("NotesNavigation")).IsVisible && shell.CurrentPage == shell.Notes && shell.Notes.IsEmpty, "Right-side notebook navigation opens the independent Notes page and helpful empty state");
        shell.NewItemCommand.Execute(null); Check(dialogs.NoteEditorCalls == 1, "Ctrl+N context command creates a study note when Notes is active");
        var beforeProgress = shell.Dashboard.Progress;
        var vm = new NoteEditorViewModel(shell, null) { Heading = "Section 44AB — Tax Audit", Body = "Study turnover limits.\nLine two, apostrophe's and Unicode ₹." };
        var form = new EditorWindow(vm, () => false) { Owner = window }; form.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); form.UpdateLayout();
        Check(System.Windows.Input.Keyboard.FocusedElement is TextBox heading && System.Windows.Automation.AutomationProperties.GetName(heading) == "Note heading", "New note editor automatically focuses the heading field");
        Check(vm.NoteDate == DateTime.Today && form.InputBindings.OfType<System.Windows.Input.KeyBinding>().Any(k => k.Key == System.Windows.Input.Key.S && k.Modifiers == System.Windows.Input.ModifierKeys.Control), "Note editor defaults to today and supplies Ctrl+S");
        await Capture(form, "10-note-editor", 760, 820);
        vm.SaveCommand.Execute(null); vm.SaveCommand.Execute(null);
        await Until(() => store.Snapshot.Notes.Count == 1 && !form.IsVisible);
        Check(store.Snapshot.Notes[0].Body == vm.Body && store.Snapshot.Notes[0].Heading == vm.Heading, "Saving multiline study notes preserves content and duplicate submissions create only one record");
        var empty = new NoteEditorViewModel(shell, null) { Heading = "GST Registration", Body = "" }; empty.SaveCommand.Execute(null); await Until(() => store.Snapshot.Notes.Count == 2);
        Check(store.Snapshot.Notes.All(n => n.NoteDate == today) && shell.Dashboard.Progress == beforeProgress, "Multiple same-date notes and empty bodies are supported without changing work statistics");
        var invalid = new NoteEditorViewModel(shell, null); invalid.SaveCommand.Execute(null); await Until(() => invalid.HasError);
        Check(invalid.Error.Contains("heading") && store.Snapshot.Notes.Count == 2, "Blank study-note heading is rejected");
        var guarded = new EditorWindow(new NoteEditorViewModel(shell, store.Snapshot.Notes[0]), () => false) { Owner = window }; guarded.Show();
        var guardVm = (NoteEditorViewModel)guarded.DataContext; var originalBody = guardVm.Body; guardVm.Body += "unsaved"; guarded.Close();
        Check(guarded.IsVisible, "Closing a note with unsaved changes respects the discard warning"); guardVm.Body = originalBody; guarded.Close();
        var badDateVm = new NoteEditorViewModel(shell, store.Snapshot.Notes[0]); var badDate = new EditorWindow(badDateVm, () => true) { Owner = window }; badDate.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); badDate.UpdateLayout(); Children<DatePickerTextBox>(badDate).Single().Text = "invalid";
        badDateVm.SaveCommand.Execute(null); await Until(() => badDateVm.HasError);
        Check(badDateVm.Error.Contains("valid date"), "Invalid typed study-note date blocks saving"); badDate.Close();
        shell.Navigate("Notes"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
        var search = Children<TextBox>(window).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Search study notes");
        search.Text = "44ab"; Check(shell.Notes.Items.Count == 1, "Note search matches headings instantly and ignores case");
        search.Text = "TURNOVER"; Check(shell.Notes.Items.Count == 1, "Note search also matches body text"); search.Text = "";
        shell.Navigate("Calendar"); shell.Calendar.Date = DateTime.Today; shell.Calendar.ClientFilter = null;
        foreach (var mode in Enum.GetValues<CalendarMode>())
        {
            shell.Calendar.Mode = mode; shell.Calendar.DisplayFilter = CalendarDisplayFilter.Everything;
            if (mode == CalendarMode.Month) Check(shell.Calendar.Days.Single(d => d.Date == today).ExtraCount == 3, "Mixed month-calendar overflow counts both work deadlines and study notes");
            shell.Calendar.DisplayFilter = CalendarDisplayFilter.StudyNotes;
            Check(shell.Calendar.Days.Sum(d => d.Items.Count) == 2 && shell.Calendar.Days.SelectMany(d => d.Items).All(e => e.IsStudyNote && e.Color == "#8B5CF6" && e.StatusLabel == ""), $"{mode} displays purple notes without work statuses");
            shell.Calendar.DisplayFilter = CalendarDisplayFilter.ClientDeadlines;
            Check(shell.Calendar.Days.SelectMany(d => d.Items).All(e => e.IsWork), $"{mode} Client Deadlines Only hides notes");
            shell.Calendar.DisplayFilter = CalendarDisplayFilter.Everything; shell.Calendar.ClientFilter = store.Snapshot.Clients.First(c => c.Id != cid).Id;
            Check(shell.Calendar.Days.Sum(d => d.Items.Count) == 2, $"{mode} client filter keeps personal study notes visible"); shell.Calendar.ClientFilter = null;
        }
        shell.Calendar.DisplayFilter = CalendarDisplayFilter.StudyNotes; shell.Calendar.Mode = CalendarMode.Day;
        var entry = shell.Calendar.Days.Single().Items.First(e => e.Name.Contains("44AB")); entry.OpenCommand.Execute(null);
        Check(dialogs.LastNote?.Heading == vm.Heading, "Clicking a calendar note opens the corresponding full study note");
        var noteId = dialogs.LastNote!.Id;
        var edited = new NoteEditorViewModel(shell, dialogs.LastNote) { Heading = "Revised Audit", Body = "Edited body\nLine two", NoteDate = today.AddDays(1).ToDateTime(TimeOnly.MinValue) };
        edited.SaveCommand.Execute(null); await Until(() => store.Snapshot.Notes.Single(n => n.Id == noteId).NoteDate == today.AddDays(1));
        Check(shell.Calendar.Days.Single().Items.All(e => e.Id != noteId), "Changing a note date immediately removes its previous calendar entry");
        shell.Calendar.Date = today.AddDays(1).ToDateTime(TimeOnly.MinValue);
        Check(shell.Calendar.Days.Single().Items.Single().Name == "Revised Audit" && shell.Notes.Items[0].Id == noteId, "Date edits move the note to the new day and update newest-first sorting");
        dialogs.AllowDelete = false; await shell.DeleteNoteAsync(store.Snapshot.Notes.Single(n => n.Id == noteId));
        Check(shell.Calendar.Days.Single().Items.Count == 1, "Cancelling note deletion preserves the note and calendar entry");
        dialogs.AllowDelete = true; await shell.DeleteNoteAsync(store.Snapshot.Notes.Single(n => n.Id == noteId));
        Check(shell.Calendar.Days.Single().IsEmpty && store.Snapshot.WorkAreas.Count == 5, "Confirmed note deletion removes its calendar entry and preserves client work");
        await store.SaveNoteAsync(null, "Section 44AB — Tax Audit", "Study turnover limits.\nLine two.", today);
        shell.Calendar.Date = DateTime.Today; shell.Calendar.DisplayFilter = CalendarDisplayFilter.Everything; shell.Calendar.Mode = CalendarMode.Week;
        shell.Clients.TableView = true; await store.ReloadAsync();
        var prefs = new ShellViewModel(new ApplicationStore(new Repository(store.Repository.DatabasePath)));
        await prefs.Store.ReloadAsync();
        Check(prefs.Calendar.Mode == CalendarMode.Week && prefs.Calendar.DisplayFilter == CalendarDisplayFilter.Everything && prefs.Clients.TableView, "Calendar and client-view preferences persist across application restart");
        var backup = Path.Combine(folder, "manual-export.db"); await shell.Settings.ExportAsync(backup);
        Check(File.Exists(backup) && shell.Settings.Message.Contains("saved"), "Settings Export Backup creates a verified complete database");
        var asks = dialogs.ConfirmCalls; var bad = Path.Combine(folder, "invalid.db"); File.WriteAllText(bad, "invalid backup"); await shell.Settings.RestoreAsync(bad);
        Check(dialogs.ConfirmCalls == asks && shell.Settings.Message.Contains("healthy"), "Settings rejects a corrupt backup before asking for replacement confirmation");
        dialogs.AllowDelete = false; await shell.Settings.RestoreAsync(backup);
        Check(shell.Settings.Message.Contains("cancelled"), "Cancelled Settings restore keeps current data"); dialogs.AllowDelete = true;
        await store.SaveNoteAsync(null, "Temporary after export", "", today); await shell.Settings.RestoreAsync(backup);
        Check(store.Snapshot.Notes.Count == 2 && shell.Settings.Message.Contains("verified") && store.Snapshot.WorkAreas.Count == 5, "Settings restore replaces data only after confirmation and refreshes every connected view");
        shell.Clients.TableView = false; shell.Calendar.Mode = CalendarMode.Month;
    }
    private sealed class TestDialogs : IDialogService
    {
        public WorkRow? LastWork
        {
            get; private set;
        }
        public bool AllowDelete { get; set; } = true;
        public void EditNote(StudyNote? note) { LastNote = note; NoteEditorCalls++; }
        public int NoteEditorCalls { get; private set; }
        public int ConfirmCalls { get; private set; }
        public StudyNote? LastNote { get; private set; }
        public void EditClient(Client? client)
        {
        }
        public void EditWork(WorkRow? work, long? clientId = null) => LastWork = work;
        public bool Confirm(string title, string description) { ConfirmCalls++; return AllowDelete; }
    }
    private sealed class CaptureTrace : TraceListener
    {
        public override void Write(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                bindingErrors.Add(message);
        }
        public override void WriteLine(string? message) => Write(message);
    }
}
