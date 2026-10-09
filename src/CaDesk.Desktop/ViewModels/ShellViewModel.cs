using CaDesk.Core;
using System.Windows.Input;
namespace CaDesk.Desktop.ViewModels;

public interface IDialogService
{
    void EditClient(Client? client);
    void EditNote(StudyNote? note);
    void EditWork(WorkRow? work, long? clientId = null);
    bool Confirm(string title, string description);
}
public sealed class ShellViewModel : Observable
{
    public ApplicationStore Store
    {
        get;
    }
    public IDialogService Dialogs { get; set; } = null!;
    public DashboardViewModel Dashboard
    {
        get;
    }
    public ClientsViewModel Clients
    {
        get;
    }
    public WorkTableViewModel Master
    {
        get;
    }
    public CalendarViewModel Calendar
    {
        get;
    }
    public SettingsViewModel Settings
    {
        get;
    }
    public NotesViewModel Notes { get; }
    public ICommand AddNoteCommand { get; }
    public ICommand NewItemCommand { get; }
    private object currentPage = null!;
    public object CurrentPage
    {
        get => currentPage; private set => Set(ref currentPage, value);
    }
    private string section = "Dashboard";
    public string Section
    {
        get => section; private set => Set(ref section, value);
    }
    private string message = "";
    public string Message
    {
        get => message; set
        {
            Set(ref message, value);
            Raise(nameof(HasMessage));
        }
    }
    public bool HasMessage => Message.Length > 0;
    private bool busy;
    public bool IsBusy
    {
        get => busy; set => Set(ref busy, value);
    }
    public string TodayLabel => DateTime.Today.ToString("dddd, d MMMM yyyy");
    public ICommand NavigateCommand
    {
        get;
    }
    public ICommand AddClientCommand
    {
        get;
    }
    public ICommand DismissMessageCommand
    {
        get;
    }
    public ICommand RefreshCommand
    {
        get;
    }
    public ShellViewModel(ApplicationStore store)
    {
        Store = store;
        Dashboard = new(this);
        Clients = new(this);
        Master = new(this);
        Calendar = new(this);
        Notes = new(this);
        AddNoteCommand = new RelayCommand(_ => Dialogs.EditNote(null));
        Settings = new(this);
        CurrentPage = Dashboard;
        NavigateCommand = new RelayCommand(p => Navigate(p?.ToString() ?? "Dashboard"));
        AddClientCommand = new RelayCommand(_ => Dialogs.EditClient(null));
        NewItemCommand = new RelayCommand(_ => { if (Section == "Notes") AddNoteCommand.Execute(null); else AddClientCommand.Execute(null); });
        DismissMessageCommand = new RelayCommand(_ => Message = "");
        RefreshCommand = new AsyncCommand(async () => await Store.ReloadAsync(), ReportError);
        store.Changed += (_, _) => Refresh();
    }
    public void Navigate(string name)
    {
        Section = name;
        CurrentPage = name switch
        {
            "Clients" => Clients,
            "Calendar" => Calendar,
            "Master Work Table" => Master,
            "Settings" => Settings,
            "Notes" => Notes,
            _ => Dashboard
        };
        Refresh();
    }
    public void OpenClient(long id)
    {
        if (Store.Snapshot.Clients.All(c => c.Id != id))
            return;
        Section = "Clients";
        CurrentPage = new WorkTableViewModel(this, id);
        ((WorkTableViewModel)CurrentPage).Refresh();
    }
    public void OpenWork(long id)
    {
        var row = Store.Snapshot.Rows.FirstOrDefault(w => w.Id == id);
        if (row != null)
            Dialogs.EditWork(row);
    }
    public void OpenNote(long id)
    {
        var note = Store.Snapshot.Notes.FirstOrDefault(n => n.Id == id);
        if (note != null) Dialogs.EditNote(note);
    }
    public async Task DeleteNoteAsync(StudyNote note)
    {
        if (Dialogs.Confirm("Delete study note?", $"Delete {note.Heading}? This cannot be undone."))
            await RunAsync(() => Store.DeleteNoteAsync(note.Id));
    }
    public void FilterMaster(WorkStatus? status = null, DeadlineFilter deadline = DeadlineFilter.All)
    {
        Master.ApplyFilter(status, deadline);
        Navigate("Master Work Table");
    }
    public void Refresh()
    {
        if (!preferencesReady) ApplyPreferences();
        Dashboard.Refresh();
        Clients.Refresh();
        Master.Refresh();
        Calendar.Refresh();
        Notes.Refresh();
        if (CurrentPage is WorkTableViewModel table && table != Master)
        {
            if (!Store.Snapshot.Clients.Any(c => c.Id == table.FixedClientId))
            {
                Navigate("Clients");
                return;
            }
            table.Refresh();
        }
        Raise(nameof(TodayLabel));
    }
    private bool preferencesReady, applyingPreferences;
    public void ApplyPreferences()
    {
        applyingPreferences = true;
        try
        {
            var p = Store.Snapshot.Preferences;
            Calendar.Mode = p.TryGetValue("calendar.mode", out var mode) && Enum.TryParse<CalendarMode>(mode, out var m) && Enum.IsDefined(m) ? m : CalendarMode.Month;
            Calendar.DisplayFilter = p.TryGetValue("calendar.display", out var display) && Enum.TryParse<CalendarDisplayFilter>(display, out var d) && Enum.IsDefined(d) ? d : CalendarDisplayFilter.Everything;
            Clients.TableView = p.TryGetValue("clients.table", out var table) && bool.TryParse(table, out var t) && t;
            preferencesReady = true;
        }
        finally { applyingPreferences = false; }
    }
    public void PersistPreference(string key, string value)
    {
        if (preferencesReady && !applyingPreferences) _ = RunAsync(() => Store.SavePreferenceAsync(key, value));
    }
    public void ReportError(Exception ex) => Message = ex is ValidationException ? ex.Message : "Your change could not be saved. Check that the local data folder is writable and has free space, then try again. Your previously saved data is kept.";
    public async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) { ReportError(ex); Refresh(); }
    }
    public async Task DeleteClientAsync(Client c)
    {
        if (Dialogs.Confirm("Delete client?", $"Delete {c.Name} and all of its work areas? This cannot be undone."))
            await RunAsync(() => Store.DeleteClientAsync(c.Id));
    }
    public async Task DeleteWorkAsync(WorkRow w)
    {
        if (Dialogs.Confirm("Delete work area?", $"Delete {w.Name} for {w.ClientName}? This cannot be undone."))
            await RunAsync(() => Store.DeleteWorkAsync(w.Id));
    }
}