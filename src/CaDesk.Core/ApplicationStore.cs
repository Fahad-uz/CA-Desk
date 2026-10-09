namespace CaDesk.Core;
public sealed class ApplicationStore(Repository repository)
{
    private readonly SemaphoreSlim gate=new(1,1);
    public Repository Repository {get;}=repository;
    public Snapshot Snapshot {get;private set;}=new(Array.Empty<Client>(),Array.Empty<WorkArea>());
    public event EventHandler? Changed;
    public Task ReloadAsync()=>MutateAsync(()=>{});
    public Task SaveClientAsync(long? id,string name,string description)=>MutateAsync(()=>Repository.SaveClient(id,name,description));
    public Task SaveWorkAsync(long? id,long clientId,string name,WorkStatus status,DateOnly? deadline,string notes)=>MutateAsync(()=>Repository.SaveWork(id,clientId,name,status,deadline,notes));
    public Task DeleteClientAsync(long id)=>MutateAsync(()=>Repository.DeleteClient(id));
    public Task DeleteWorkAsync(long id)=>MutateAsync(()=>Repository.DeleteWork(id));
    private async Task MutateAsync(Action operation)
    {
        await gate.WaitAsync();
        try {Snapshot=await Task.Run(()=>{operation();return Repository.Load();});}
        finally {gate.Release();}
        Changed?.Invoke(this,EventArgs.Empty);
    }
}
