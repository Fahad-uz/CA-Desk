using CaDesk.Core;
using System.Windows.Input;
namespace CaDesk.Desktop.ViewModels;

public sealed class ClientCardViewModel(Client client, IEnumerable<WorkArea> works, ShellViewModel shell)
{
    public Client Client { get; } = client;
    public long Id => Client.Id;
    public string Name => Client.Name;
    public string Description => Client.Description;
    public string DueDateLabel => Client.DueDate?.ToString("d MMM yyyy") ?? "No due date";
    public string DaysRemainingLabel => ClientDueDates.Remaining(Client);
    public bool IsOverdue => Client.DaysRemaining(DateOnly.FromDateTime(DateTime.Today)) < 0;
    public string DueColor => IsOverdue ? "#DC2626" : "#64748B";
    public string Initials => string.Concat(Client.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s => s[0])).ToUpperInvariant();
    public Progress Progress { get; } = Progress.From(works);
    public ICommand OpenCommand { get; } = new RelayCommand(_ => shell.OpenClient(client.Id));
    public ICommand EditCommand { get; } = new RelayCommand(_ => shell.Dialogs.EditClient(client));
    public ICommand DeleteCommand { get; } = new AsyncCommand(() => shell.DeleteClientAsync(client), shell.ReportError);
}
public sealed class ClientsViewModel(ShellViewModel shell) : Observable
{
    private string search = "";
    public string Search
    {
        get => search; set
        {
            if (Set(ref search, value))
                Refresh();
        }
    }
    private bool tableView;
    public bool TableView
    {
        get => tableView; set
        {
            if (Set(ref tableView, value)) shell.PersistPreference("clients.table", value.ToString());
            Raise(nameof(CardView));
        }
    }
    public bool CardView => !TableView;
    public IReadOnlyList<ClientCardViewModel> Items { get; private set; } = [];
    public bool IsEmpty => Items.Count == 0;
    public string ResultLabel => $"{Items.Count} of {shell.Store.Snapshot.Clients.Count} clients";
    public ICommand AddCommand => shell.AddClientCommand;
    public ICommand ClearSearchCommand => new RelayCommand(_ => Search = "");
    public void Refresh()
    {
        var groups = shell.Store.Snapshot.WorkAreas.ToLookup(w => w.ClientId);
        Items = shell.Store.Snapshot.Clients.Where(c => c.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase)).Select(c => new ClientCardViewModel(c, groups[c.Id], shell)).ToArray();
        Raise(nameof(Items));
        Raise(nameof(IsEmpty));
        Raise(nameof(ResultLabel));
    }
}

public static class ClientDueDates
{
    public static string Remaining(Client client) => client.DaysRemaining(DateOnly.FromDateTime(DateTime.Today)) switch
    {
        null => "", 0 => "Due today", 1 => "1 day remaining", -1 => "Overdue by 1 day",
        < 0 and var days => $"Overdue by {-days} days", var days => $"{days} days remaining"
    };
}
