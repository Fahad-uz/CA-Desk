using CaDesk.Core;
using System.Windows.Input;
namespace CaDesk.Desktop.ViewModels;

public enum CalendarMode
{
    Month, Week, Day
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
    public IReadOnlyList<WorkItemViewModel> Items
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
    public CalendarDay(DateOnly date, DateOnly selected, IEnumerable<WorkRow> works, CalendarViewModel calendar, ShellViewModel shell, bool limit)
    {
        Date = date;
        IsCurrentMonth = date.Month == selected.Month;
        var rows = works.OrderBy(w => w.Status).ThenBy(w => w.Name).ToArray();
        Items = (limit ? rows.Take(3) : rows).Select(w => new WorkItemViewModel(w, shell)).ToArray();
        ExtraCount = limit ? Math.Max(0, rows.Length - 3) : 0;
        HasOverdue = rows.Any(w => w.IsOverdue(DateOnly.FromDateTime(DateTime.Today)));
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
            if (Set(ref mode, value))
                Refresh();
        }
    }
    public bool IsMonth => Mode == CalendarMode.Month;
    public bool IsAgenda => !IsMonth;
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
    public IReadOnlyList<ClientOption> ClientOptions { get; private set; } = [];
    public IReadOnlyList<CalendarDay> Days { get; private set; } = [];
    public string Title => Mode switch { CalendarMode.Month => Date.ToString("MMMM yyyy"), CalendarMode.Week => $"Week of {WorkQueries.WeekStart(DateOnly.FromDateTime(Date)):d MMM yyyy}", _ => Date.ToString("dddd, d MMMM yyyy") };
    public string ResultLabel => $"{Days.Sum(d => d.Items.Count + d.ExtraCount)} deadlines in view";
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
        var grouped = shell.Store.Snapshot.Rows.Where(w => w.Deadline != null && (client == null || w.Client.Id == client)).ToLookup(w => w.Deadline!.Value);
        Days = Enumerable.Range(0, count).Select(i => new CalendarDay(start.AddDays(i), selected, grouped[start.AddDays(i)], this, shell, IsMonth)).ToArray();
        foreach (var p in new[] { nameof(Days), nameof(Title), nameof(IsMonth), nameof(IsAgenda), nameof(ResultLabel) })
            Raise(p);
    }
}
