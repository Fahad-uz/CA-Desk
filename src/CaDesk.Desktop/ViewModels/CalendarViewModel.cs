using CaDesk.Core;
using System.Windows.Input;
namespace CaDesk.Desktop.ViewModels;

public enum CalendarMode
{
    Month, Week, Day
}
public enum CalendarDisplayFilter { Everything, ClientDeadlines, StudyNotes }
public sealed record CalendarDisplayOption(CalendarDisplayFilter Value, string Label);
public sealed class CalendarEventViewModel
{
    public long Id { get; }
    public bool IsStudyNote { get; }
    public bool IsWork => !IsStudyNote;
    public string Name { get; }
    public string ClientName { get; } = "";
    public string StatusLabel { get; } = "";
    public string DeadlineLabel { get; } = "";
    public string CalendarLabel { get; }
    public DateOnly EventDate { get; }
    public bool IsOverdue { get; }
    public string Color { get; }
    public ICommand OpenCommand { get; }
    public ICommand OpenClientCommand { get; }
    public CalendarEventViewModel(WorkRow row, ShellViewModel shell)
    {
        Id = row.Id; Name = row.Name; ClientName = row.ClientName; StatusLabel = Options.StatusName(row.Status);
        EventDate = row.Deadline!.Value; DeadlineLabel = EventDate.ToString("d MMM yyyy");
        IsOverdue = row.IsOverdue(DateOnly.FromDateTime(DateTime.Today));
        Color = IsOverdue ? "#DC2626" : row.Status switch { WorkStatus.Completed => "#16A34A", WorkStatus.InProgress => "#2563EB", _ => "#64748B" };
        CalendarLabel = $"{Name} · {ClientName} · {StatusLabel}{(IsOverdue ? " · Overdue" : "")}";
        OpenCommand = new RelayCommand(_ => shell.OpenWork(Id));
        OpenClientCommand = new RelayCommand(_ => shell.OpenClient(row.Client.Id));
    }
    public CalendarEventViewModel(StudyNote note, ShellViewModel shell)
    {
        Id = note.Id; Name = CalendarLabel = note.Heading; EventDate = note.NoteDate; IsStudyNote = true; Color = "#8B5CF6";
        OpenCommand = new RelayCommand(_ => shell.OpenNote(Id)); OpenClientCommand = OpenCommand;
    }
}
public sealed class CalendarDay
{
    public DateOnly Date
    {
        get;
    }
    public string Label => Date.Day.ToString();
    public string FullLabel => Date.ToString("ddd, d MMM");
    public bool IsToday => Date == DateOnly.FromDateTime(DateTime.Today);
    public bool HasOverdue
    {
        get;
    }
    public bool IsCurrentMonth
    {
        get;
    }
    public IReadOnlyList<CalendarEventViewModel> Items
    {
        get;
    }
    public int ExtraCount
    {
        get;
    }
    public bool HasMore => ExtraCount > 0;
    public string MoreLabel => $"+{ExtraCount} more";
    public bool IsEmpty => Items.Count == 0;
    public ICommand OpenDayCommand
    {
        get;
    }
    public CalendarDay(DateOnly date, DateOnly selected, IEnumerable<CalendarEventViewModel> events, CalendarViewModel calendar, ShellViewModel shell, bool limit)
    {
        Date = date;
        IsCurrentMonth = date.Month == selected.Month;
        var rows = events.OrderBy(e => e.IsStudyNote).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id).ToArray();
        Items = (limit ? rows.Take(3) : rows).ToArray();
        ExtraCount = limit ? Math.Max(0, rows.Length - 3) : 0;
        HasOverdue = rows.Any(w => w.IsOverdue);
        OpenDayCommand = new RelayCommand(_ => calendar.ShowDay(Date));
    }
}
public sealed class CalendarViewModel : Observable
{
    private readonly ShellViewModel shell;
    private CalendarMode mode;
    public CalendarMode Mode
    {
        get => mode; set
        {
            if (Set(ref mode, value)) { shell.PersistPreference("calendar.mode", value.ToString()); Refresh(); }
        }
    }
    public bool IsMonth => Mode == CalendarMode.Month;
    public bool IsAgenda => !IsMonth;
    public static IReadOnlyList<CalendarDisplayOption> DisplayOptions { get; } = [new(CalendarDisplayFilter.Everything, "Show Everything"), new(CalendarDisplayFilter.ClientDeadlines, "Client Deadlines Only"), new(CalendarDisplayFilter.StudyNotes, "Study Notes Only")];
    private CalendarDisplayFilter displayFilter;
    public CalendarDisplayFilter DisplayFilter { get => displayFilter; set { if (Set(ref displayFilter, value)) { shell.PersistPreference("calendar.display", value.ToString()); Refresh(); } } }
    private DateTime date = DateTime.Today;
    public DateTime Date
    {
        get => date; set
        {
            if (Set(ref date, value))
                Refresh();
        }
    }
    private long? client;
    public long? ClientFilter
    {
        get => client; set
        {
            if (Set(ref client, value))
                Refresh();
        }
    }
    public ClientOption? SelectedClient
    {
        get => ClientOptions.FirstOrDefault(o => o.Value == ClientFilter) ?? ClientOptions.FirstOrDefault(); set => ClientFilter = value?.Value;
    }
    public IReadOnlyList<ClientOption> ClientOptions { get; private set; } = [];
    public IReadOnlyList<CalendarDay> Days { get; private set; } = [];
    public string Title => Mode switch { CalendarMode.Month => Date.ToString("MMMM yyyy"), CalendarMode.Week => $"Week of {WorkQueries.WeekStart(DateOnly.FromDateTime(Date)):d MMM yyyy}", _ => Date.ToString("dddd, d MMMM yyyy") };
    public string ResultLabel => $"{Days.Sum(d => d.Items.Count + d.ExtraCount)} events in view";
    public ICommand PreviousCommand
    {
        get;
    }
    public ICommand NextCommand
    {
        get;
    }
    public ICommand TodayCommand
    {
        get;
    }
    public ICommand ViewCommand
    {
        get;
    }
    public CalendarViewModel(ShellViewModel shell)
    {
        this.shell = shell;
        PreviousCommand = new RelayCommand(_ => Move(-1));
        NextCommand = new RelayCommand(_ => Move(1));
        TodayCommand = new RelayCommand(_ => Date = DateTime.Today);
        ViewCommand = new RelayCommand(p => Mode = Enum.Parse<CalendarMode>(p?.ToString() ?? "Month"));
    }
    private void Move(int amount) => Date = Mode switch { CalendarMode.Month => Date.AddMonths(amount), CalendarMode.Week => Date.AddDays(7 * amount), _ => Date.AddDays(amount) };
    public void ShowDay(DateOnly day)
    {
        date = day.ToDateTime(TimeOnly.MinValue);
        mode = CalendarMode.Day;
        shell.PersistPreference("calendar.mode", mode.ToString());
        Raise(nameof(Date));
        Raise(nameof(Mode));
        Refresh();
    }
    public void Refresh()
    {
        var next = new[] { new ClientOption(null, "All Clients") }.Concat(shell.Store.Snapshot.Clients.Select(c => new ClientOption(c.Id, c.Name))).ToArray();
        if (!ClientOptions.SequenceEqual(next))
        {
            ClientOptions = next;
            Raise(nameof(ClientOptions));
        }
        if (client is { } cid && !shell.Store.Snapshot.Clients.Any(c => c.Id == cid))
        {
            client = null;
            Raise(nameof(ClientFilter));
        }
        var selected = DateOnly.FromDateTime(Date);
        var start = Mode switch
        {
            CalendarMode.Month => WorkQueries.WeekStart(new(selected.Year, selected.Month, 1)),
            CalendarMode.Week => WorkQueries.WeekStart(selected),
            _ => selected
        };
        var count = Mode switch
        {
            CalendarMode.Month => 42,
            CalendarMode.Week => 7,
            _ => 1
        };
        IEnumerable<CalendarEventViewModel> events = [];
        if (DisplayFilter != CalendarDisplayFilter.StudyNotes)
            events = shell.Store.Snapshot.Rows.Where(w => w.Deadline != null && (client == null || w.Client.Id == client)).Select(w => new CalendarEventViewModel(w, shell));
        if (DisplayFilter != CalendarDisplayFilter.ClientDeadlines)
            events = events.Concat(shell.Store.Snapshot.Notes.Select(n => new CalendarEventViewModel(n, shell)));
        var grouped = events.ToLookup(e => e.EventDate);
        Days = Enumerable.Range(0, count).Select(i => new CalendarDay(start.AddDays(i), selected, grouped[start.AddDays(i)], this, shell, IsMonth)).ToArray();
        foreach (var p in new[] { nameof(Days), nameof(Title), nameof(IsMonth), nameof(IsAgenda), nameof(ResultLabel), nameof(SelectedClient) })
            Raise(p);
    }
}
