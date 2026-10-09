namespace CaDesk.Core;

public sealed class ApplicationStore(Repository repository)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public Repository Repository { get; } = repository;
    public Snapshot Snapshot { get; private set; } = new(Array.Empty<Client>(), Array.Empty<WorkArea>());
    public event EventHandler? Changed;
    public Task ReloadAsync() => MutateAsync(() => { });
    public Task SaveClientAsync(long? id, string name, string description, DateOnly? dueDate = null) => MutateAsync(() => Repository.SaveClient(id, name, description, dueDate));
    public Task SaveWorkAsync(long? id, long clientId, string name, WorkStatus status, DateOnly? deadline, string notes) => MutateAsync(() => Repository.SaveWork(id, clientId, name, status, deadline, notes));
    public Task UpdateStatusAsync(long id, WorkStatus status) => MutateAsync(() => Repository.UpdateStatus(id, status));
    public Task UpdateDeadlineAsync(long id, DateOnly? date) => MutateAsync(() => Repository.UpdateDeadline(id, date));
    public Task DeleteClientAsync(long id) => MutateAsync(() => Repository.DeleteClient(id));
    public Task DeleteWorkAsync(long id) => MutateAsync(() => Repository.DeleteWork(id));
    public Task SaveNoteAsync(long? id, string heading, string body, DateOnly date) => MutateAsync(() => Repository.SaveNote(id, heading, body, date));
    public Task DeleteNoteAsync(long id) => MutateAsync(() => Repository.DeleteNote(id));
    public Task SavePreferenceAsync(string key, string value) => MutateAsync(() => Repository.SavePreference(key, value));
    public Task RestoreBackupAsync(string path) => MutateAsync(() => Repository.RestoreBackup(path));
    public Task ExportBackupAsync(string path) => MaintenanceAsync(() => Repository.ExportBackup(path));
    public Task BackupIfDueAsync() => MaintenanceAsync(() => Repository.BackupIfDue());
    public async Task ValidateBackupAsync(string path) => await MaintenanceAsync(() => Repository.ValidateBackup(path));
    private async Task MaintenanceAsync(Action action)
    {
        await gate.WaitAsync();
        try { await Task.Run(action); }
        finally { gate.Release(); }
    }
    private async Task MutateAsync(Action operation)
    {
        await gate.WaitAsync();
        try
        {
            Snapshot = await Task.Run(() => { operation(); return Repository.Load(); });
        }
        finally { gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
