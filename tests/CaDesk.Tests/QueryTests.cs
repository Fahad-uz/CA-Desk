using CaDesk.Core;
using Xunit;
namespace CaDesk.Tests;
public class QueryTests
{
    private static readonly DateOnly Today=new(2026,10,9);
    private static WorkRow R(long id,string name,WorkStatus status,DateOnly? date,string client="Acme")=>new(new(id,id,name,status,date,"",DateTime.UtcNow,DateTime.UtcNow),new(id,client,"",DateTime.UtcNow));
    private static WorkRow[] Rows=>[R(1,"GST filing",WorkStatus.InProgress,Today.AddDays(-1)),R(2,"Audit",WorkStatus.Completed,Today.AddDays(-4)),R(3,"GST audit",WorkStatus.NotStarted,Today),R(4,"Accounts",WorkStatus.InProgress,null,"Beta"),R(5,"Review",WorkStatus.NotStarted,Today.AddDays(3))];
    [Fact] public void OverdueDoesNotChangeStatusAndExcludesCompleted(){var rows=WorkQueries.Filter(Rows,new(Deadline:DeadlineFilter.Overdue),Today);Assert.Equal(1,Assert.Single(rows).Id);Assert.Equal(WorkStatus.InProgress,rows[0].Status);}
    [Fact] public void SearchIsPartialCaseInsensitiveAndCombinesWithFilters(){Assert.Equal(1,Assert.Single(WorkQueries.Filter(Rows,new("gSt",WorkStatus.InProgress,DeadlineFilter.Overdue,1),Today)).Id);Assert.Equal(4,Assert.Single(WorkQueries.Filter(Rows,new("bet"),Today)).Id);}
    [Theory] [InlineData(DeadlineFilter.All,5)] [InlineData(DeadlineFilter.Overdue,1)] [InlineData(DeadlineFilter.Today,1)] [InlineData(DeadlineFilter.ThisWeek,3)] [InlineData(DeadlineFilter.ThisMonth,4)] [InlineData(DeadlineFilter.NoDeadline,1)]
    public void DeadlineFiltersHaveExpectedResults(DeadlineFilter filter,int count)=>Assert.Equal(count,WorkQueries.Filter(Rows,new(Deadline:filter),Today).Count);
    [Theory] [InlineData(WorkSort.Deadline)] [InlineData(WorkSort.Client)] [InlineData(WorkSort.Status)] [InlineData(WorkSort.Name)]
    public void SortsWorkBothWays(WorkSort sort){var asc=WorkQueries.Filter(Rows,new(Sort:sort),Today);var desc=WorkQueries.Filter(Rows,new(Sort:sort,Descending:true),Today);Assert.Equal(5,asc.Count);Assert.Equal(5,desc.Count);if(sort==WorkSort.Deadline){Assert.Equal(2,asc[0].Id);Assert.Equal(5,desc[0].Id);Assert.Null(asc[^1].Deadline);Assert.Null(desc[^1].Deadline);}}
    [Fact] public void NoMatchingResultsIsEmpty()=>Assert.Empty(WorkQueries.Filter(Rows,new("missing"),Today));
    [Fact] public void ProgressUsesOnlyCompletedAndHandlesZero(){var p=Progress.From(Rows.Select(r=>r.Work));Assert.Equal(20,p.Percentage);Assert.Equal(2,p.InProgress);Assert.Equal("No work areas",Progress.From([]).Label);}
    [Fact] public void UpcomingIncludesOverdueAndExcludesCompletedAndUndated()=>Assert.Equal(new long[]{1,3,5},WorkQueries.Upcoming(Rows).Select(r=>r.Id));
    [Fact] public void WeekStartsMondayAcrossYearBoundary()=>Assert.Equal(new DateOnly(2025,12,29),WorkQueries.WeekStart(new(2026,1,1)));
}
