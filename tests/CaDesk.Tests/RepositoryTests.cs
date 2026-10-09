using CaDesk.Core;
using Xunit;
namespace CaDesk.Tests;
public sealed class RepositoryTests : IDisposable
{
    private readonly string folder=Path.Combine(Path.GetTempPath(),"CaDeskTests",Guid.NewGuid().ToString("N"));
    private Repository New()=>new(Path.Combine(folder,"test.db"));
    public void Dispose(){if(Directory.Exists(folder))Directory.Delete(folder,true);}
    [Fact] public void EmptyDatabaseHasNoSampleRecords(){var s=New().Load();Assert.Empty(s.Clients);Assert.Empty(s.WorkAreas);}
    [Fact] public void ClientCreateRenameAndDescriptionPersist(){var r=New();var id=r.SaveClient(null,"  Acme  ","notes");r.SaveClient(id,"Acme Limited","revised");var c=Assert.Single(New().Load().Clients);Assert.Equal("Acme Limited",c.Name);Assert.Equal("revised",c.Description);}
    [Fact] public void DuplicateNamesAreFriendlyAndUnicodeAware(){var r=New();r.SaveClient(null,"École","");var e=Assert.Throws<ValidationException>(()=>r.SaveClient(null," école ",""));Assert.Contains("already exists",e.Message);Assert.Single(r.Load().Clients);}
    [Fact] public void BlankClientAndWorkNamesAreRejected(){var r=New();Assert.Throws<ValidationException>(()=>r.SaveClient(null," ",""));var id=r.SaveClient(null,"A","");Assert.Throws<ValidationException>(()=>r.SaveWork(null,id," "));}
    [Theory] [InlineData(WorkStatus.NotStarted)] [InlineData(WorkStatus.InProgress)] [InlineData(WorkStatus.Completed)]
    public void EveryStatusPersists(WorkStatus status){var r=New();var c=r.SaveClient(null,"A","");var w=r.SaveWork(null,c,"GST");Assert.Equal(WorkStatus.NotStarted,r.Load().WorkAreas[0].Status);r.SaveWork(w,c,"GST",status);Assert.Equal(status,New().Load().WorkAreas[0].Status);}
    [Fact] public void WorkNameDeadlineAndMultilineNotesCanBeEditedAndCleared(){var r=New();var c=r.SaveClient(null,"A","");var w=r.SaveWork(null,c,"GST",deadline:new(2026,10,9),notes:"line 1\nline 2");var first=New().Load().WorkAreas[0];Assert.Equal(new DateOnly(2026,10,9),first.Deadline);Assert.Equal("line 1\nline 2",first.Notes);r.SaveWork(w,c,"Tax audit",WorkStatus.InProgress,new(2026,11,1),"edited");var second=New().Load().WorkAreas[0];Assert.Equal("Tax audit",second.Name);Assert.Equal(new DateOnly(2026,11,1),second.Deadline);Assert.Equal("edited",second.Notes);r.SaveWork(w,c,second.Name,second.Status,null,"");var last=New().Load().WorkAreas[0];Assert.Null(last.Deadline);Assert.Empty(last.Notes);}
    [Fact] public void ForeignKeyAndInvalidStatusAreRejected(){var r=New();Assert.Throws<ValidationException>(()=>r.SaveWork(null,42,"GST"));var c=r.SaveClient(null,"A","");Assert.Throws<ValidationException>(()=>r.SaveWork(null,c,"GST",(WorkStatus)3));}
    [Fact] public void DeletingClientCascadesOnlyItsOwnWork(){var r=New();var a=r.SaveClient(null,"A","");var b=r.SaveClient(null,"B","");r.SaveWork(null,a,"GST");r.SaveWork(null,a,"Audit");r.SaveWork(null,b,"GST");r.DeleteClient(a);Assert.Single(r.Load().Clients);Assert.Equal(b,Assert.Single(r.Load().WorkAreas).ClientId);}
    [Fact] public void WorkDeletionPersists(){var r=New();var c=r.SaveClient(null,"A","");var w=r.SaveWork(null,c,"GST");r.DeleteWork(w);Assert.Empty(New().Load().WorkAreas);Assert.Single(r.Load().Clients);}
    [Fact] public void ParametersKeepSqlLikeNamesAndLongNotesIntact(){var r=New();var c=r.SaveClient(null,"O'Brien; DROP TABLE clients;--","");var note=new string('x',100000)+"\nlast line";r.SaveWork(null,c,"Director's audit",notes:note);Assert.Equal(note,New().Load().WorkAreas[0].Notes);}
    [Fact] public async Task StorePublishesCommittedDataAndSurvivesRestart(){var store=new ApplicationStore(New());var changes=0;store.Changed+=(_,_)=>changes++;await store.SaveClientAsync(null,"A","");await store.SaveWorkAsync(null,store.Snapshot.Clients[0].Id,"GST",WorkStatus.Completed,null,"");Assert.Equal(2,changes);Assert.Equal(100,Progress.From(store.Snapshot.WorkAreas).Percentage);Assert.Single(New().Load().WorkAreas);}
    [Fact] public async Task LargeDatasetCanLoadSearchAndCalculateProgress(){var r=New();for(var i=0;i<100;i++){var c=r.SaveClient(null,$"Client {i:D3}","");for(var j=0;j<20;j++)r.SaveWork(null,c,$"Work {j:D2}",(WorkStatus)(j%3));}var store=new ApplicationStore(r);await store.ReloadAsync();Assert.Equal(2000,store.Snapshot.WorkAreas.Count);Assert.Equal(20,WorkQueries.Filter(store.Snapshot.Rows,new("Client 005"),new(2026,10,9)).Count);}
}
