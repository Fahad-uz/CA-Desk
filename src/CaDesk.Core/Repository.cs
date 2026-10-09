using Microsoft.Data.Sqlite;
using System.Globalization;
namespace CaDesk.Core;

public sealed class ValidationException(string message) : Exception(message);
public sealed class Repository
{
    private readonly string connectionString;
    public string DatabasePath
    {
        get;
    }
    public string BackupDirectory { get; }
    public Repository(string path, string? backupDirectory = null)
    {
        DatabasePath = Path.GetFullPath(path);
        BackupDirectory = Path.GetFullPath(backupDirectory ?? Path.Combine(Path.GetDirectoryName(DatabasePath)!, "Backups"));
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath, ForeignKeys = true, DefaultTimeout = 10, Pooling = false }.ToString();
        var existing = File.Exists(DatabasePath);
        using var db = Open();
        using var version = db.CreateCommand();
        version.CommandText = "PRAGMA user_version";
        var current = Convert.ToInt32(version.ExecuteScalar());
        if (current > DatabaseSafety.LatestSchema)
            throw new ValidationException("This database was created by a newer CA Desk version. Please update CA Desk.");
        if (existing) DatabaseSafety.Validate(db);
        string? backup = current is > 0 and < DatabaseSafety.LatestSchema ? CreateBackup("pre-migration") : null;
        try
        {
            if (current < DatabaseSafety.LatestSchema)
            {
                using var tx = db.BeginTransaction();
                using var cmd = db.CreateCommand();
                cmd.Transaction = tx;
                if (current < 1)
                {
                    cmd.CommandText = """
                        CREATE TABLE clients(id INTEGER PRIMARY KEY, name TEXT NOT NULL CHECK(length(trim(name))>0), normalized_name TEXT NOT NULL UNIQUE, description TEXT NOT NULL DEFAULT '', created_at TEXT NOT NULL);
                        CREATE TABLE work_areas(id INTEGER PRIMARY KEY, client_id INTEGER NOT NULL REFERENCES clients(id) ON DELETE CASCADE, name TEXT NOT NULL CHECK(length(trim(name))>0), status INTEGER NOT NULL DEFAULT 0 CHECK(status IN(0,1,2)), deadline TEXT, notes TEXT NOT NULL DEFAULT '', created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
                        CREATE INDEX ix_work_client ON work_areas(client_id);
                        CREATE INDEX ix_work_deadline ON work_areas(deadline);
                        """;
                    cmd.ExecuteNonQuery();
                }
                if (current < 2)
                {
                    cmd.CommandText = "ALTER TABLE clients ADD COLUMN due_date TEXT";
                    cmd.ExecuteNonQuery();
                }
                if (current < 3)
                {
                    cmd.CommandText = """
                        CREATE TABLE study_notes(id INTEGER PRIMARY KEY, heading TEXT NOT NULL CHECK(length(trim(heading))>0), body TEXT NOT NULL DEFAULT '', note_date TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
                        CREATE INDEX ix_study_note_date ON study_notes(note_date);
                        CREATE TABLE preferences(key TEXT PRIMARY KEY, value TEXT NOT NULL);
                        """;
                    cmd.ExecuteNonQuery();
                }
                DatabaseSafety.Validate(db, tx, DatabaseSafety.LatestSchema);
                cmd.CommandText = $"PRAGMA user_version={DatabaseSafety.LatestSchema}";
                cmd.ExecuteNonQuery();
                tx.Commit();
            }
        }
        catch (Exception ex) when (backup != null)
        {
            throw new RecoveryException("CA Desk could not finish updating your database. The update was rolled back and a verified backup is available. Restore it for use with a compatible release?", backup, ex);
        }
        using var wal = db.CreateCommand();
        wal.CommandText = "PRAGMA journal_mode=WAL";
        wal.ExecuteScalar();
    }
    private SqliteConnection Open()
    {
        var db = new SqliteConnection(connectionString);
        db.Open();
        return db;
    }
    private static string Timestamp() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    private static DateTime ParseTime(string text) => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    public Snapshot Load()
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        var clients = new List<Client>();
        var works = new List<WorkArea>();
        var notes = new List<StudyNote>();
        var preferences = new Dictionary<string, string>();
        using (var cmd = db.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT id,name,description,created_at,due_date FROM clients ORDER BY name COLLATE NOCASE,id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                clients.Add(new(r.GetInt64(0), r.GetString(1), r.GetString(2), ParseTime(r.GetString(3)), r.IsDBNull(4) ? null : DateOnly.ParseExact(r.GetString(4), "yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
        using (var cmd = db.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT id,client_id,name,status,deadline,notes,created_at,updated_at FROM work_areas";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                works.Add(new(r.GetInt64(0), r.GetInt64(1), r.GetString(2), (WorkStatus)r.GetInt32(3), r.IsDBNull(4) ? null : DateOnly.ParseExact(r.GetString(4), "yyyy-MM-dd", CultureInfo.InvariantCulture), r.GetString(5), ParseTime(r.GetString(6)), ParseTime(r.GetString(7))));
        }
        using (var cmd = db.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT id,heading,body,note_date,created_at,updated_at FROM study_notes ORDER BY note_date DESC,id DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read()) notes.Add(new(r.GetInt64(0), r.GetString(1), r.GetString(2), DateOnly.ParseExact(r.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture), ParseTime(r.GetString(4)), ParseTime(r.GetString(5))));
        }
        using (var cmd = db.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT key,value FROM preferences";
            using var r = cmd.ExecuteReader();
            while (r.Read()) preferences.Add(r.GetString(0), r.GetString(1));
        }
        tx.Commit();
        return new(clients, works, notes, preferences);
    }
    public long SaveClient(long? id, string name, string? description, DateOnly? dueDate = null)
    {
        name = name.Trim();
        if (name.Length == 0)
            throw new ValidationException("Please enter a client name.");
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = id is null ? "INSERT INTO clients(name,normalized_name,description,created_at,due_date) VALUES($name,$normal,$description,$created,$due) RETURNING id" : "UPDATE clients SET name=$name, normalized_name=$normal, description=$description,due_date=$due WHERE id=$id RETURNING id";
        cmd.Parameters.AddWithValue("$id", (object?)id ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$normal", name.ToUpperInvariant());
        cmd.Parameters.AddWithValue("$description", description ?? "");
        cmd.Parameters.AddWithValue("$created", Timestamp());
        cmd.Parameters.AddWithValue("$due", (object?)dueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value);
        try
        {
            return Convert.ToInt64(cmd.ExecuteScalar() ?? throw new ValidationException("This client no longer exists. Please refresh and try again."));
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067) { throw new ValidationException("A client with this name already exists. Please choose a different name."); }
    }
    public long SaveWork(long? id, long clientId, string name, WorkStatus status = WorkStatus.NotStarted, DateOnly? deadline = null, string? notes = null)
    {
        name = name.Trim();
        if (name.Length == 0)
            throw new ValidationException("Please enter a work area name.");
        if (!Enum.IsDefined(status))
            throw new ValidationException("Please choose one of the three work statuses.");
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = id is null ? "INSERT INTO work_areas(client_id,name,status,deadline,notes,created_at,updated_at) VALUES($client,$name,$status,$deadline,$notes,$now,$now) RETURNING id" : "UPDATE work_areas SET client_id=$client,name=$name,status=$status,deadline=$deadline,notes=$notes,updated_at=$now WHERE id=$id RETURNING id";
        cmd.Parameters.AddWithValue("$id", (object?)id ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$client", clientId);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$status", (int)status);
        cmd.Parameters.AddWithValue("$deadline", (object?)deadline?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", notes ?? "");
        cmd.Parameters.AddWithValue("$now", Timestamp());
        try
        {
            return Convert.ToInt64(cmd.ExecuteScalar() ?? throw new ValidationException("This work area no longer exists. Please refresh and try again."));
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 787) { throw new ValidationException("Please choose an existing client."); }
    }
    public void UpdateStatus(long id, WorkStatus status)
    {
        if (!Enum.IsDefined(status))
            throw new ValidationException("Please choose one of the three work statuses.");
        UpdateField(id, "status", (int)status);
    }
    public void UpdateDeadline(long id, DateOnly? date) => UpdateField(id, "deadline", (object?)date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value);
    private void UpdateField(long id, string column, object value)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"UPDATE work_areas SET {column}=$value,updated_at=$now WHERE id=$id";
        cmd.Parameters.AddWithValue("$value", value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$now", Timestamp());
        if (cmd.ExecuteNonQuery() != 1)
            throw new ValidationException("This work area no longer exists. Please refresh and try again.");
    }
    public long SaveNote(long? id, string heading, string? body, DateOnly noteDate)
    {
        heading = heading.Trim();
        if (heading.Length == 0) throw new ValidationException("Please enter a note heading.");
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = id is null
            ? "INSERT INTO study_notes(heading,body,note_date,created_at,updated_at) VALUES($heading,$body,$date,$now,$now) RETURNING id"
            : "UPDATE study_notes SET heading=$heading,body=$body,note_date=$date,updated_at=$now WHERE id=$id RETURNING id";
        cmd.Parameters.AddWithValue("$id", (object?)id ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$heading", heading);
        cmd.Parameters.AddWithValue("$body", body ?? "");
        cmd.Parameters.AddWithValue("$date", noteDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$now", Timestamp());
        return Convert.ToInt64(cmd.ExecuteScalar() ?? throw new ValidationException("This note no longer exists. Please refresh and try again."));
    }
    public void DeleteNote(long id) => Delete("study_notes", id);
    public void SavePreference(string key, string value)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO preferences(key,value) VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }
    public string CreateBackup(string reason = "automatic")
    {
        Directory.CreateDirectory(BackupDirectory);
        var path = Path.Combine(BackupDirectory, $"cadesk-{reason}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db");
        DatabaseSafety.Export(DatabasePath, path);
        return path;
    }
    public string? BackupIfDue(DateOnly? today = null)
    {
        var day = today ?? DateOnly.FromDateTime(DateTime.Today);
        var automatic = Directory.Exists(BackupDirectory) ? Directory.GetFiles(BackupDirectory, "cadesk-automatic-*.db").OrderByDescending(File.GetLastWriteTimeUtc).ToArray() : [];
        if (automatic.Length > 0 && DateOnly.FromDateTime(File.GetLastWriteTime(automatic[0])) >= day)
            return null;
        var path = CreateBackup();
        // Rotate only automatic copies after a new verified backup succeeds. Recovery copies remain.
        foreach (var old in automatic.Skip(13)) File.Delete(old);
        return path;
    }
    public void ExportBackup(string destination) => DatabaseSafety.Export(DatabasePath, destination);
    public int ValidateBackup(string path)
    {
        if (Path.GetFullPath(path).Equals(DatabasePath, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Choose an exported backup, rather than the database currently in use.");
        return DatabaseSafety.Validate(path);
    }
    public void RestoreBackup(string path)
    {
        ValidateBackup(path);
        var staged = Path.Combine(Path.GetDirectoryName(DatabasePath)!, $"restore-{Guid.NewGuid():N}.pending");
        string? safety = null;
        try
        {
            DatabaseSafety.Export(path, staged);
            _ = new Repository(staged, BackupDirectory); // Apply supported forward migrations to the isolated candidate.
            DatabaseSafety.Validate(staged);
            safety = CreateBackup("pre-restore");
            DatabaseSafety.Copy(staged, DatabasePath);
            DatabaseSafety.Validate(DatabasePath);
            _ = Load();
        }
        catch (Exception ex) when (safety != null)
        {
            DatabaseSafety.Recover(safety, DatabasePath);
            throw new RecoveryException("Restore did not finish. Your previous database was recovered and its backup has been kept.", safety, ex);
        }
        finally { DatabaseSafety.DeleteStaging(staged); }
    }
    public void DeleteClient(long id) => Delete("clients", id);
    public void DeleteWork(long id) => Delete("work_areas", id);
    private void Delete(string table, long id)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"DELETE FROM {table} WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}
