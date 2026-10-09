namespace CaDesk.Core;
public enum DeadlineFilter { All, Overdue, Today, ThisWeek, ThisMonth, NoDeadline }
public enum WorkSort { Deadline, Client, Status, Name }
public sealed record WorkFilter(string Search="",WorkStatus? Status=null,DeadlineFilter Deadline=DeadlineFilter.All,long? ClientId=null,WorkSort Sort=WorkSort.Deadline,bool Descending=false);
public sealed record Progress(int Total,int Completed,int InProgress,int NotStarted)
{
    public double Percentage=>Total==0?0:100d*Completed/Total;
    public string Label=>Total==0?"No work areas":$"{Percentage:0}% complete · {Completed}/{Total}";
    public static Progress From(IEnumerable<WorkArea> works) {var a=works.ToArray();return new(a.Length,a.Count(w=>w.Status==WorkStatus.Completed),a.Count(w=>w.Status==WorkStatus.InProgress),a.Count(w=>w.Status==WorkStatus.NotStarted));}
}
public static class WorkQueries
{
    public static DateOnly WeekStart(DateOnly date)=>date.AddDays(-(((int)date.DayOfWeek+6)%7));
    public static IReadOnlyList<WorkRow> Filter(IEnumerable<WorkRow> rows,WorkFilter filter,DateOnly today)
    {
        var search=filter.Search.Trim();var week=WeekStart(today);
        var query=rows.Where(r=>(search.Length==0||r.Name.Contains(search,StringComparison.OrdinalIgnoreCase)||r.ClientName.Contains(search,StringComparison.OrdinalIgnoreCase)) && (filter.Status is null||r.Status==filter.Status) && (filter.ClientId is null||r.Client.Id==filter.ClientId) && (filter.Deadline switch {
            DeadlineFilter.Overdue=>r.IsOverdue(today), DeadlineFilter.Today=>r.Deadline==today,
            DeadlineFilter.ThisWeek=>r.Deadline>=week&&r.Deadline<week.AddDays(7),
            DeadlineFilter.ThisMonth=>r.Deadline is {} d&&d.Year==today.Year&&d.Month==today.Month,
            DeadlineFilter.NoDeadline=>r.Deadline is null,_=>true }));
        IOrderedEnumerable<WorkRow> sorted=filter.Sort switch {
            WorkSort.Client=>filter.Descending?query.OrderByDescending(r=>r.ClientName,StringComparer.OrdinalIgnoreCase):query.OrderBy(r=>r.ClientName,StringComparer.OrdinalIgnoreCase),
            WorkSort.Status=>filter.Descending?query.OrderByDescending(r=>r.Status):query.OrderBy(r=>r.Status),
            WorkSort.Name=>filter.Descending?query.OrderByDescending(r=>r.Name,StringComparer.OrdinalIgnoreCase):query.OrderBy(r=>r.Name,StringComparer.OrdinalIgnoreCase),
            _=>filter.Descending?query.OrderBy(r=>r.Deadline is null).ThenByDescending(r=>r.Deadline):query.OrderBy(r=>r.Deadline is null).ThenBy(r=>r.Deadline) };
        return sorted.ThenBy(r=>r.ClientName,StringComparer.OrdinalIgnoreCase).ThenBy(r=>r.Id).ToArray();
    }
    public static IReadOnlyList<WorkRow> Upcoming(IEnumerable<WorkRow> rows,int count=6)=>rows.Where(r=>r.Status!=WorkStatus.Completed&&r.Deadline!=null).OrderBy(r=>r.Deadline).ThenBy(r=>r.Id).Take(count).ToArray();
}
