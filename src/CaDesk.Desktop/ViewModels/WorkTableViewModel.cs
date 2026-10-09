using CaDesk.Core;
using System.Windows.Input;
namespace CaDesk.Desktop.ViewModels;

public sealed class WorkItemViewModel : Observable
{
    private readonly ShellViewModel shell;
    public WorkRow Row
    {
        get;
    }
    public void ReportInvalidDate() => shell.ReportError(new ValidationException(WorkEditorViewModel.InvalidDateMessage));
    public long Id => Row.Id;
    public string ClientName => Row.ClientName;
    public string Name => Row.Name;
    public string Notes => Row.Notes;
    public bool HasNotes => !string.IsNullOrEmpty(Notes);
    public string NotesPreview => HasNotes ? Notes.Replace('\n', ' ').Replace('\r', ' ') : "—";
    public bool IsOverdue => Row.IsOverdue(DateOnly.FromDateTime(DateTime.Today));
    public string DeadlineLabel => Row.Deadline?.ToString("dd MMM yyyy") ?? "No deadline";
    public string CalendarLabel => $"{ClientName} · {Name} · {StatusLabel} · {DeadlineLabel}{(IsOverdue ? " · Overdue" : "")}";
    public string StatusLabel => Options.StatusName(Status);
    private WorkStatus status;
    public WorkStatus Status
    {
        get => status; set
        {
            if (Set(ref status, value))
            {
                Raise(nameof(StatusLabel));
                _ = shell.RunAsync(() => shell.Store.UpdateStatusAsync(Id, value));
            }
        }
    }
    private DateTime? deadline;
    public DateTime? Deadline
    {
        get => deadline; set
        {
            if (Set(ref deadline, value))
                _ = shell.RunAsync(() => shell.Store.UpdateDeadlineAsync(Id, value is { } d ? DateOnly.FromDateTime(d) : null));
        }
    }
    public ICommand OpenCommand
    {
        get;
    }
    public ICommand OpenClientCommand
    {
        get;
    }
    public ICommand DeleteCommand
    {
        get;
    }
    public ICommand ClearDeadlineCommand
    {
        get;
    }
    public WorkItemViewModel(WorkRow row, ShellViewModel shell)
    {
        Row = row;
        this.shell = shell;
        status = row.Status;
        deadline = row.Deadline?.ToDateTime(TimeOnly.MinValue);
        OpenCommand = new RelayCommand(_ => shell.OpenWork(Id));
        OpenClientCommand = new RelayCommand(_ => shell.OpenClient(Row.Client.Id));
        DeleteCommand = new AsyncCommand(() => shell.DeleteWorkAsync(Row), shell.ReportError);
        ClearDeadlineCommand = new RelayCommand(_ => Deadline = null);
    }
}
public sealed class WorkTableViewModel : Observable
{
    private readonly ShellViewModel shell;
    public long? FixedClientId
    {
        get;
    }
    public bool IsWorkspace => FixedClientId != null;
    public bool IsMaster => !IsWorkspace;
    private string search = "";
    public string Search
    {
        get => search; set
        {
            if (Set(ref search, value))
                Refresh();
        }
    }
    private WorkStatus? status;
    public WorkStatus? StatusFilter
    {
        get => status; set
        {
            if (Set(ref status, value))
            {
                Raise(nameof(SelectedStatusFilter));
                Refresh();
            }
        }
    }
    public StatusFilterOption? SelectedStatusFilter
    {
        get => Options.StatusFilters.First(o => o.Value == StatusFilter); set => StatusFilter = value?.Value;
    }
    public ClientOption? SelectedClient
    {
        get => ClientOptions.FirstOrDefault(o => o.Value == ClientFilter) ?? ClientOptions.FirstOrDefault(); set => ClientFilter = value?.Value;
    }
    private DeadlineFilter deadline;
    public DeadlineFilter DeadlineFilter
    {
        get => deadline; set
        {
            if (Set(ref deadline, value))
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
    public IReadOnlyList<WorkItemViewModel> Items { get; private set; } = [];
    public Progress Progress { get; private set; } = Progress.From([]);
    public string Title => FixedClientId is { } id ? shell.Store.Snapshot.Clients.FirstOrDefault(c => c.Id == id)?.Name ?? "Client workspace" : "Master Work Table";
    public string Subtitle => FixedClientId is { } id ? shell.Store.Snapshot.Clients.FirstOrDefault(c => c.Id == id)?.Description ?? "" : "Every client. Every work area. One clear view.";
    public string ClientDueLabel
    {
        get
        {
            var c = shell.Store.Snapshot.Clients.FirstOrDefault(c => c.Id == FixedClientId);
            return c?.DueDate is { } date ? $"Client due date: {date:d MMM yyyy} · {ClientDueDates.Remaining(c)}" : "No client due date";
        }
    }
    public string ClientDueColor => shell.Store.Snapshot.Clients.FirstOrDefault(c => c.Id == FixedClientId)?.DaysRemaining(DateOnly.FromDateTime(DateTime.Today)) < 0 ? "#DC2626" : "#64748B";
    public string ResultLabel => $"Showing {Items.Count} of {TotalCount} work areas";
    public int TotalCount => FixedClientId is { } id ? shell.Store.Snapshot.WorkAreas.Count(w => w.ClientId == id) : shell.Store.Snapshot.WorkAreas.Count;
    public bool IsEmpty => Items.Count == 0;
    private WorkSort sort = WorkSort.Deadline; private bool descending;
    public string SortLabel => $"Sorted by {sort.ToString().ToLowerInvariant()} {(descending ? "↓" : "↑")}";
    public ICommand AddCommand
    {
        get;
    }
    public ICommand EditClientCommand
    {
        get;
    }
    public ICommand BackCommand
    {
        get;
    }
    public ICommand ResetCommand
    {
        get;
    }
    public ICommand ClearSearchCommand
    {
        get;
    }
    public WorkTableViewModel(ShellViewModel shell, long? clientId = null)
    {
        this.shell = shell;
        FixedClientId = clientId;
        AddCommand = new RelayCommand(_ => { if (shell.Store.Snapshot.Clients.Count == 0) { shell.Message = "Add a client first, then create their work areas."; shell.Navigate("Clients"); return; } shell.Dialogs.EditWork(null, FixedClientId); });
        EditClientCommand = new RelayCommand(_ => shell.Dialogs.EditClient(shell.Store.Snapshot.Clients.FirstOrDefault(c => c.Id == FixedClientId)));
        BackCommand = new RelayCommand(_ => shell.Navigate("Clients"));
        ResetCommand = new RelayCommand(_ => ApplyFilter(null, Core.DeadlineFilter.All));
        ClearSearchCommand = new RelayCommand(_ => Search = "");
    }
    public void ApplyFilter(WorkStatus? value, DeadlineFilter due)
    {
        search = "";
        status = value;
        deadline = due;
        client = null;
        Raise(nameof(Search));
        Raise(nameof(StatusFilter));
        Raise(nameof(DeadlineFilter));
        Raise(nameof(ClientFilter));
        Refresh();
    }
    public void SortBy(WorkSort value)
    {
        descending = sort == value && !descending;
        sort = value;
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
        Items = WorkQueries.Filter(shell.Store.Snapshot.Rows, new(Search, StatusFilter, DeadlineFilter, FixedClientId ?? ClientFilter, sort, descending), DateOnly.FromDateTime(DateTime.Today)).Select(row => new WorkItemViewModel(row, shell)).ToArray();
        Progress = Progress.From(shell.Store.Snapshot.WorkAreas.Where(w => FixedClientId == null || w.ClientId == FixedClientId));
        foreach (var p in new[] { nameof(Items), nameof(Progress), nameof(Title), nameof(Subtitle), nameof(ClientDueLabel), nameof(ClientDueColor), nameof(ResultLabel), nameof(IsEmpty), nameof(SortLabel), nameof(SelectedStatusFilter), nameof(SelectedClient) })
            Raise(p);
    }
}
