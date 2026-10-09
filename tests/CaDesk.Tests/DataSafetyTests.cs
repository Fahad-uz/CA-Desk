using CaDesk.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CaDesk.Tests;

public sealed class DataSafetyTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "CaDeskSafety", Guid.NewGuid().ToString("N"));
    private Repository New() => new(Path.Combine(folder, "current.db"));
    public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    private static void CreateLegacy(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var db = new SqliteConnection($"Data Source={path};Pooling=False");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE clients(id INTEGER PRIMARY KEY,name TEXT NOT NULL,normalized_name TEXT NOT NULL UNIQUE,description TEXT NOT NULL,created_at TEXT NOT NULL);
            CREATE TABLE work_areas(id INTEGER PRIMARY KEY,client_id INTEGER NOT NULL REFERENCES clients(id) ON DELETE CASCADE,name TEXT NOT NULL,status INTEGER NOT NULL,deadline TEXT,notes TEXT NOT NULL,created_at TEXT NOT NULL,updated_at TEXT NOT NULL);
            INSERT INTO clients VALUES(42,'Sample firm','SAMPLE FIRM','Original description','2026-01-01T00:00:00Z');
            INSERT INTO work_areas VALUES(84,42,'Audit',1,'2026-10-09','Original work note'||char(10)||'second line','2026-01-01T00:00:00Z','2026-02-01T00:00:00Z');
            PRAGMA user_version=1;
            PRAGMA journal_mode=WAL;
            """;
        cmd.ExecuteNonQuery();
    }
    [Fact]
    public void LegacyFolderImportsOnceAndPreservesEveryField()
    {
        var paths = new AppDataPaths(folder);
        CreateLegacy(paths.LegacyPath);
        var r = paths.OpenRepository();
        var s = r.Load();
        var c = Assert.Single(s.Clients);
        Assert.Equal(42, c.Id); Assert.Equal("Original description", c.Description); Assert.Null(c.DueDate);
        var w = Assert.Single(s.WorkAreas);
        Assert.Equal(84, w.Id); Assert.Equal(42, w.ClientId); Assert.Equal(WorkStatus.InProgress, w.Status);
        Assert.Equal(new DateOnly(2026, 10, 9), w.Deadline); Assert.Equal("Original work note\nsecond line", w.Notes);
        Assert.Empty(s.Notes);
        Assert.Equal(3, DatabaseSafety.Validate(paths.DatabasePath));
        Assert.Equal(1, DatabaseSafety.Validate(paths.LegacyPath));
        var backup = Assert.Single(Directory.GetFiles(paths.BackupDirectory, "*pre-migration*.db"));
        Assert.Equal(1, DatabaseSafety.Validate(backup));
        r.SaveClient(null, "New client", "");
        Assert.Equal(2, paths.OpenRepository().Load().Clients.Count);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void FailedMigrationRollsBackAndProvidesRecoverableBackup(int schema)
    {
        var path = Path.Combine(folder, "current.db"); CreateLegacy(path);
        using (var db = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            db.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = (schema == 2 ? "ALTER TABLE clients ADD COLUMN due_date TEXT; PRAGMA user_version=2; " : "") + "CREATE TABLE study_notes(collision TEXT)"; cmd.ExecuteNonQuery();
        }
        var error = Assert.Throws<RecoveryException>(() => New());
        Assert.Equal(schema, DatabaseSafety.Validate(path));
        Assert.Equal(schema, DatabaseSafety.Validate(error.BackupPath));
        DatabaseSafety.Recover(error.BackupPath, path);
        Assert.Equal(schema, DatabaseSafety.Validate(path));
        using var verify = new SqliteConnection($"Data Source={path};Pooling=False"); verify.Open();
        using var columns = verify.CreateCommand(); columns.CommandText = "SELECT count(*) FROM pragma_table_info('clients') WHERE name='due_date'";
        Assert.Equal(schema == 1 ? 0L : 1L, columns.ExecuteScalar());
    }
    [Fact]
    public void SchemaTwoUpgradeRetainsExistingDueDates()
    {
        var path = Path.Combine(folder, "current.db"); CreateLegacy(path);
        using (var db = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            db.Open(); using var cmd = db.CreateCommand();
            cmd.CommandText = "ALTER TABLE clients ADD COLUMN due_date TEXT; UPDATE clients SET due_date='2027-02-15'; PRAGMA user_version=2"; cmd.ExecuteNonQuery();
        }
        var r = New(); Assert.Equal(new DateOnly(2027, 2, 15), Assert.Single(r.Load().Clients).DueDate);
        Assert.Equal(2, DatabaseSafety.Validate(Assert.Single(Directory.GetFiles(r.BackupDirectory, "*pre-migration*.db"))));
        Assert.Empty(r.Load().Notes);
    }
    [Fact]
    public void NewerDatabaseIsRejectedWithoutMutation()
    {
        var r = New();
        using (var db = new SqliteConnection($"Data Source={r.DatabasePath};Pooling=False"))
        { db.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "PRAGMA user_version=99"; cmd.ExecuteNonQuery(); }
        Assert.Throws<ValidationException>(() => New());
        using var check = new SqliteConnection($"Data Source={r.DatabasePath};Pooling=False"); check.Open();
        using var version = check.CreateCommand(); version.CommandText = "PRAGMA user_version";
        Assert.Equal(99L, version.ExecuteScalar());
    }
    [Fact]
    public void ExistingUnknownDatabaseIsNeverInitialized()
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "current.db"); File.WriteAllText(path, "unrelated file");
        Assert.ThrowsAny<Exception>(() => New());
        Assert.Equal("unrelated file", File.ReadAllText(path));
    }
    [Fact]
    public void DueDatePersistsAndUsesCalendarDays()
    {
        var r = New(); var today = new DateOnly(2026, 10, 9);
        var id = r.SaveClient(null, "A", "", today.AddDays(2));
        Assert.Equal(2, Assert.Single(New().Load().Clients).DaysRemaining(today));
        r.SaveClient(id, "A", "", today); Assert.Equal(0, r.Load().Clients[0].DaysRemaining(today));
        r.SaveClient(id, "A", "", today.AddDays(-3)); Assert.Equal(-3, r.Load().Clients[0].DaysRemaining(today));
        r.SaveClient(id, "A", "", null); Assert.Null(New().Load().Clients[0].DueDate);
    }
    [Fact]
    public void NotesCrudMultilineEmptyBodySameDateAndRestart()
    {
        var r = New(); var date = new DateOnly(2026, 10, 9);
        var id = r.SaveNote(null, "  Tax Audit  ", "first\nsecond", date);
        r.SaveNote(null, "GST", "", date);
        Assert.Equal(2, New().Load().Notes.Count);
        var original = r.Load().Notes.Single(n => n.Id == id);
        r.SaveNote(id, "Revised", "third\nfourth", date.AddYears(-1));
        var changed = New().Load().Notes.Single(n => n.Id == id);
        Assert.Equal(original.CreatedAt, changed.CreatedAt); Assert.Equal(date.AddYears(-1), changed.NoteDate);
        Assert.Equal("third\nfourth", changed.Body); Assert.Equal("GST", r.Load().Notes[0].Heading);
        r.DeleteNote(id); Assert.Single(New().Load().Notes);
        Assert.Throws<ValidationException>(() => r.SaveNote(null, " ", "", date));
    }
    [Fact]
    public void NotesNeverAffectProgressOrCascadeWithClient()
    {
        var r = New(); var c = r.SaveClient(null, "A", "");
        r.SaveWork(null, c, "Audit", WorkStatus.Completed, notes: "work note");
        var before = Progress.From(r.Load().WorkAreas);
        r.SaveNote(null, "Personal", "Study body", new(2026, 10, 9));
        Assert.Equal(before, Progress.From(r.Load().WorkAreas));
        r.DeleteClient(c); Assert.Single(r.Load().Notes); Assert.Empty(r.Load().WorkAreas);
    }
    [Fact]
    public void ExportRestoreIncludesNotesDueDatesPreferencesAndSafetyCopy()
    {
        var r = New(); var c = r.SaveClient(null, "A", "client note", new(2026, 10, 10));
        r.SaveWork(null, c, "Audit", WorkStatus.Completed, new(2026, 11, 1), "work note\nline");
        r.SaveNote(null, "Study", "body\nline", new(2026, 10, 9)); r.SavePreference("calendar.mode", "Week");
        var snapshot = r.Load(); var backup = Path.Combine(folder, "export.db"); r.ExportBackup(backup);
        r.DeleteClient(c); r.DeleteNote(snapshot.Notes[0].Id); r.SavePreference("calendar.mode", "Day");
        r.RestoreBackup(backup); var after = New().Load();
        Assert.Equal(snapshot.Clients, after.Clients); Assert.Equal(snapshot.WorkAreas, after.WorkAreas); Assert.Equal(snapshot.Notes, after.Notes);
        Assert.Equal("Week", after.Preferences["calendar.mode"]);
        var safety = Assert.Single(Directory.GetFiles(r.BackupDirectory, "*pre-restore*.db"));
        var old = new Repository(safety).Load(); Assert.Empty(old.Clients); Assert.Empty(old.Notes);
    }
    [Fact]
    public void LegacyBackupRestoresWithForwardMigration()
    {
        var r = New(); var old = Path.Combine(folder, "old.db"); CreateLegacy(old);
        r.RestoreBackup(old); Assert.Equal(42, Assert.Single(r.Load().Clients).Id);
        Assert.Equal(3, DatabaseSafety.Validate(r.DatabasePath)); Assert.Equal(1, DatabaseSafety.Validate(old));
    }
    [Fact]
    public void InvalidOrFutureBackupCannotReplaceCurrentData()
    {
        var r = New(); r.SaveClient(null, "Kept", "");
        var bad = Path.Combine(folder, "bad.db"); File.WriteAllText(bad, "bad");
        Assert.Throws<ValidationException>(() => r.RestoreBackup(bad));
        var future = Path.Combine(folder, "future.db"); r.ExportBackup(future);
        using (var db = new SqliteConnection($"Data Source={future};Pooling=False"))
        { db.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "PRAGMA user_version=99"; cmd.ExecuteNonQuery(); }
        Assert.Throws<ValidationException>(() => r.RestoreBackup(future));
        Assert.Equal("Kept", Assert.Single(r.Load().Clients).Name);
        Assert.Throws<ValidationException>(() => r.ExportBackup(r.DatabasePath));
    }
    [Fact]
    public void BackupCapturesUncheckpointedWalWrites()
    {
        var r = New();
        using var writer = new SqliteConnection($"Data Source={r.DatabasePath};Pooling=False"); writer.Open();
        using var cmd = writer.CreateCommand(); cmd.CommandText = "PRAGMA wal_autocheckpoint=0; INSERT INTO clients(name,normalized_name,description,created_at) VALUES('In WAL','IN WAL','','2026-01-01T00:00:00Z')"; cmd.ExecuteNonQuery();
        var backup = Path.Combine(folder, "wal-export.db"); r.ExportBackup(backup);
        Assert.Equal("In WAL", Assert.Single(new Repository(backup).Load().Clients).Name);
    }
    [Fact]
    public void DailyBackupDoesNotRepeatAndRetentionKeepsRecoveryCopies()
    {
        var r = New(); r.CreateBackup("pre-migration");
        for (var i = 0; i < 17; i++)
        {
            var path = r.CreateBackup(); File.SetLastWriteTime(path, DateTime.Today.AddDays(-20 + i));
        }
        Assert.NotNull(r.BackupIfDue()); Assert.Null(r.BackupIfDue());
        Assert.Equal(14, Directory.GetFiles(r.BackupDirectory, "cadesk-automatic-*.db").Length);
        Assert.Single(Directory.GetFiles(r.BackupDirectory, "*pre-migration*.db"));
    }
    [Fact]
    public async Task ConcurrentStoreWritesAndBackupRemainConsistent()
    {
        var store = new ApplicationStore(New());
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => store.SaveNoteAsync(null, $"Note {i}", "", new(2026, 10, 9))));
        await store.ExportBackupAsync(Path.Combine(folder, "store.db"));
        Assert.Equal(20, new Repository(Path.Combine(folder, "store.db")).Load().Notes.Count);
    }
}
