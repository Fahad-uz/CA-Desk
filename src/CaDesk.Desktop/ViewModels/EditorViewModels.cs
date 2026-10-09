using CaDesk.Core;
using System.Windows.Input;
namespace CaDesk.Desktop.ViewModels;
public abstract class EditorViewModel : Observable
{
    protected readonly ShellViewModel Shell;
    public event EventHandler? Saved;
    private bool saving;
    public bool IsSaving {get=>saving;private set{Set(ref saving,value);Raise(nameof(CanEdit));CommandManager.InvalidateRequerySuggested();}}
    public bool CanEdit=>!IsSaving;
    protected async Task<bool> SaveOnceAsync()
    {
        if(IsSaving)return false;IsSaving=true;
        try {await SaveAsync();return true;} finally {IsSaving=false;}
    }
    private string error="";
    public string Error {get=>error;set{Set(ref error,value);Raise(nameof(HasError));}}
    public bool HasError=>Error.Length>0;
    public abstract string Title {get;}
    public ICommand SaveCommand {get;}
    protected EditorViewModel(ShellViewModel shell){Shell=shell;SaveCommand=new AsyncCommand(async()=>{Error="";if(await SaveOnceAsync())Saved?.Invoke(this,EventArgs.Empty);},ReportError);}
    protected void ReportError(Exception ex)=>Error=ex is ValidationException?ex.Message:"This change could not be saved. Check the data folder and available disk space, then try again.";
    protected abstract Task SaveAsync();
    protected void Complete()=>Saved?.Invoke(this,EventArgs.Empty);
}
public sealed class ClientEditorViewModel : EditorViewModel
{
    private readonly long? id;
    public override string Title=>id is null?"Add client":"Edit client";
    private string name;
    public string Name {get=>name;set=>Set(ref name,value);}
    private string description;
    public string Description {get=>description;set=>Set(ref description,value);}
    public ClientEditorViewModel(ShellViewModel shell,Client? client):base(shell){id=client?.Id;name=client?.Name??"";description=client?.Description??"";}
    protected override Task SaveAsync()=>Shell.Store.SaveClientAsync(id,Name,Description);
}
public sealed class WorkEditorViewModel : EditorViewModel
{
    private readonly long? id;
    public override string Title=>id is null?"Add work area":"Work area details";
    public IReadOnlyList<ClientOption> Clients {get;}
    private long? clientId;
    public long? ClientId {get=>clientId;set=>Set(ref clientId,value);}
    private string name;
    public string Name {get=>name;set=>Set(ref name,value);}
    private WorkStatus status;
    public WorkStatus Status {get=>status;set=>Set(ref status,value);}
    private DateTime? deadline;
    public DateTime? Deadline {get=>deadline;set=>Set(ref deadline,value);}
    private string notes;
    public string Notes {get=>notes;set=>Set(ref notes,value);}
    public ICommand ClearDeadlineCommand {get;}
    public ICommand OpenClientCommand {get;}
    public WorkEditorViewModel(ShellViewModel shell,WorkRow? row,long? selectedClient):base(shell)
    {
        id=row?.Id;Clients=shell.Store.Snapshot.Clients.Select(c=>new ClientOption(c.Id,c.Name)).ToArray();clientId=row?.Client.Id??selectedClient??Clients.FirstOrDefault()?.Value;
        name=row?.Name??"";status=row?.Status??WorkStatus.NotStarted;deadline=row?.Deadline?.ToDateTime(TimeOnly.MinValue);notes=row?.Notes??"";
        ClearDeadlineCommand=new RelayCommand(_=>Deadline=null);
        OpenClientCommand=new AsyncCommand(async()=>{if(await SaveOnceAsync()){Complete();Shell.OpenClient(ClientId!.Value);}},ReportError);
    }
    protected override Task SaveAsync()
    {
        if(ClientId is null)throw new ValidationException("Please choose a client.");
        return Shell.Store.SaveWorkAsync(id,ClientId.Value,Name,Status,Deadline is {} d?DateOnly.FromDateTime(d):null,Notes);
    }
}
