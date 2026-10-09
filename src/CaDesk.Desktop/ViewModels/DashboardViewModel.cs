using CaDesk.Core;
using System.Windows.Input;
namespace CaDesk.Desktop.ViewModels;

public sealed record SummaryCard(string Label, int Count, string Glyph, string Color, ICommand Command);
public sealed class DashboardViewModel(ShellViewModel shell) : Observable
{
    public IReadOnlyList<SummaryCard> Cards { get; private set; } = [];
    public IReadOnlyList<ClientCardViewModel> Clients { get; private set; } = [];
    public IReadOnlyList<WorkItemViewModel> Upcoming { get; private set; } = [];
    public Progress Progress { get; private set; } = Progress.From([]);
    public bool NoUpcoming => Upcoming.Count == 0;
    public bool IsEmpty => Progress.Total == 0;
    public string NotStartedShare => Share(Progress.NotStarted);
    public string InProgressShare => Share(Progress.InProgress);
    public string CompletedShare => Share(Progress.Completed);
    private string Share(int value) => $"{(Progress.Total == 0 ? 0 : 100d * value / Progress.Total):0}%";
    public ICommand AddClientCommand => shell.AddClientCommand;
    public ICommand AllClientsCommand => new RelayCommand(_ => shell.Navigate("Clients"));
    public ICommand AllWorkCommand => new RelayCommand(_ => shell.FilterMaster());
    public void Refresh()
    {
        var data = shell.Store.Snapshot;
        Progress = Progress.From(data.WorkAreas);
        Cards = [new("Total Clients", data.Clients.Count, "\uE716", "#2563EB", new RelayCommand(_ => shell.Navigate("Clients"))), new("Work Areas", Progress.Total, "\uE8A5", "#2563EB", new RelayCommand(_ => shell.FilterMaster())), new("Not Started", Progress.NotStarted, "\uE916", "#64748B", new RelayCommand(_ => shell.FilterMaster(WorkStatus.NotStarted))), new("In Progress", Progress.InProgress, "\uE895", "#2563EB", new RelayCommand(_ => shell.FilterMaster(WorkStatus.InProgress))), new("Completed", Progress.Completed, "\uE930", "#16A34A", new RelayCommand(_ => shell.FilterMaster(WorkStatus.Completed))), new("Overdue Work", data.Rows.Count(w => w.IsOverdue(DateOnly.FromDateTime(DateTime.Today))), "\uE823", "#DC2626", new RelayCommand(_ => shell.FilterMaster(deadline: DeadlineFilter.Overdue)))];
        var groups = data.WorkAreas.ToLookup(w => w.ClientId);
        Clients = data.Clients.Select(c => new ClientCardViewModel(c, groups[c.Id], shell)).ToArray();
        Upcoming = WorkQueries.Upcoming(data.Rows).Select(w => new WorkItemViewModel(w, shell)).ToArray();
        foreach (var p in new[] { nameof(NoUpcoming), nameof(Cards), nameof(Progress), nameof(Clients), nameof(Upcoming), nameof(IsEmpty), nameof(NotStartedShare), nameof(InProgressShare), nameof(CompletedShare) })
            Raise(p);
    }
}
