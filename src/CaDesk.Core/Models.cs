namespace CaDesk.Core;

public enum WorkStatus
{
    NotStarted, InProgress, Completed
}
public sealed record Client(long Id, string Name, string Description, DateTime CreatedAt);
public sealed record WorkArea(long Id, long ClientId, string Name, WorkStatus Status, DateOnly? Deadline, string Notes, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record WorkRow(WorkArea Work, Client Client)
{
    public long Id => Work.Id;
    public string ClientName => Client.Name;
    public string Name => Work.Name;
    public WorkStatus Status => Work.Status;
    public DateOnly? Deadline => Work.Deadline;
    public string Notes => Work.Notes;
    public bool IsOverdue(DateOnly today) => Deadline is { } date && date < today && Status != WorkStatus.Completed;
}
public sealed record Snapshot(IReadOnlyList<Client> Clients, IReadOnlyList<WorkArea> WorkAreas)
{
    public IReadOnlyList<WorkRow> Rows
    {
        get
        {
            var map = Clients.ToDictionary(c => c.Id);
            return WorkAreas.Select(w => new WorkRow(w, map[w.ClientId])).ToArray();
        }
    }
}
