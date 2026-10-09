using CaDesk.Core;
using System.Windows.Input;
namespace CaDesk.Desktop.ViewModels;
public sealed class ClientCardViewModel(Client client,IEnumerable<WorkArea> works,ShellViewModel shell)
{
    public Client Client {get;}=client;
    public long Id=>Client.Id;
    public string Name=>Client.Name;
    public string Description=>Client.Description;
    public string Initials=>string.Concat(Client.Name.Split(' ',StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s=>s[0])).ToUpperInvariant();
    public Progress Progress {get;}=Progress.From(works);
    public ICommand OpenCommand {get;}=new RelayCommand(_=>shell.OpenClient(client.Id));
    public ICommand EditCommand {get;}=new RelayCommand(_=>shell.Dialogs.EditClient(client));
    public ICommand DeleteCommand {get;}=new AsyncCommand(()=>shell.DeleteClientAsync(client),shell.ReportError);
}
public sealed class ClientsViewModel(ShellViewModel shell):Observable
{
    private string search="";
    public string Search {get=>search;set{if(Set(ref search,value))Refresh();}}
    private bool tableView;
    public bool TableView {get=>tableView;set{Set(ref tableView,value);Raise(nameof(CardView));}}
    public bool CardView=>!TableView;
    public IReadOnlyList<ClientCardViewModel> Items {get;private set;}=[];
    public bool IsEmpty=>Items.Count==0;
    public string ResultLabel=>$"{Items.Count} of {shell.Store.Snapshot.Clients.Count} clients";
    public ICommand AddCommand=>shell.AddClientCommand;
    public ICommand ClearSearchCommand=>new RelayCommand(_=>Search="");
    public void Refresh()
    {
        var groups=shell.Store.Snapshot.WorkAreas.ToLookup(w=>w.ClientId);
        Items=shell.Store.Snapshot.Clients.Where(c=>c.Name.Contains(Search.Trim(),StringComparison.OrdinalIgnoreCase)).Select(c=>new ClientCardViewModel(c,groups[c.Id],shell)).ToArray();
        Raise(nameof(Items));Raise(nameof(IsEmpty));Raise(nameof(ResultLabel));
    }
}
