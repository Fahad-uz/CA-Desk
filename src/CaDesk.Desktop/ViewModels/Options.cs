using CaDesk.Core;
namespace CaDesk.Desktop.ViewModels;
public sealed record StatusOption(WorkStatus Value,string Label);
public sealed record StatusFilterOption(WorkStatus? Value,string Label);
public sealed record DeadlineOption(DeadlineFilter Value,string Label);
public sealed record ClientOption(long? Value,string Label);
public static class Options
{
    public static StatusOption[] Statuses {get;}=[new(WorkStatus.NotStarted,"Not Started"),new(WorkStatus.InProgress,"In Progress"),new(WorkStatus.Completed,"Completed")];
    public static StatusFilterOption[] StatusFilters {get;}=[new(null,"All Statuses"),new(WorkStatus.NotStarted,"Not Started"),new(WorkStatus.InProgress,"In Progress"),new(WorkStatus.Completed,"Completed")];
    public static DeadlineOption[] Deadlines {get;}=[new(DeadlineFilter.All,"All Deadlines"),new(DeadlineFilter.Overdue,"Overdue"),new(DeadlineFilter.Today,"Today"),new(DeadlineFilter.ThisWeek,"This Week"),new(DeadlineFilter.ThisMonth,"This Month"),new(DeadlineFilter.NoDeadline,"No Deadline")];
    public static string StatusName(WorkStatus status)=>Statuses[(int)status].Label;
}
