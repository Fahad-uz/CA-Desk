using Microsoft.Data.Sqlite;
using System.Globalization;

namespace CaDesk.Core;

public sealed class RecoveryException(string message, string backupPath, Exception cause) : Exception(message, cause)
{
    public string BackupPath { get; } = backupPath;
}

public static class DatabaseSafety
{
    public const int LatestSchema = 3;
    internal static SqliteConnection Open(string path, bool readOnly = false)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path), Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true, Pooling = false, DefaultTimeout = 10
        }.ToString());
        db.Open();
        return db;
    }

    public static int Validate(string path)
    {
        if (!File.Exists(path)) throw new ValidationException("The selected backup could not be found.");
        try
        {
            using var db = Open(path, true);
            return Validate(db);
        }
        catch (Exception ex) when (ex is SqliteException or FormatException or InvalidCastException)
        {
            throw new ValidationException("This file is not a healthy CA Desk database. Please select another backup.");
        }
    }

    internal static int Validate(SqliteConnection db, SqliteTransaction? tx = null, int? expectedVersion = null)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "PRAGMA integrity_check";
        using (var r = cmd.ExecuteReader())
        {
            if (!r.Read() || r.GetString(0) != "ok" || r.Read())
                throw new ValidationException("Database integrity checks failed. Your data has not been reset.");
        }
        cmd.CommandText = "PRAGMA foreign_key_check";
        using (var r = cmd.ExecuteReader())
            if (r.Read()) throw new ValidationException("This database has broken client relationships. Please select another backup.");
        cmd.CommandText = "PRAGMA user_version";
        var version = expectedVersion ?? Convert.ToInt32(cmd.ExecuteScalar());
        if (version > LatestSchema)
            throw new ValidationException("This database was created by a newer CA Desk version. Please update CA Desk.");
        if (version < 1) throw new ValidationException("This file is not a supported CA Desk database. Existing data has not been reset.");
        CheckRows(cmd, "SELECT name,created_at FROM clients", r =>
        {
            Required(r.GetString(0)); ParseTime(r.GetString(1));
        });
        CheckRows(cmd, "SELECT name,status,deadline,notes,created_at,updated_at FROM work_areas", r =>
        {
            Required(r.GetString(0));
            if (r.GetInt32(1) is < 0 or > 2) throw new ValidationException("This database contains an unsupported work status.");
            if (!r.IsDBNull(2)) ParseDate(r.GetString(2));
            _ = r.GetString(3); ParseTime(r.GetString(4)); ParseTime(r.GetString(5));
        });
        // Prepare the full column contract even for empty tables.
        CheckRows(cmd, "SELECT id,name,normalized_name,description,created_at FROM clients", _ => { });
        CheckRows(cmd, "SELECT id,client_id,name,status,deadline,notes,created_at,updated_at FROM work_areas", _ => { });
        if (version >= 2)
            CheckRows(cmd, "SELECT due_date FROM clients", r => { if (!r.IsDBNull(0)) ParseDate(r.GetString(0)); });
        if (version >= 3)
        {
            CheckRows(cmd, "SELECT id,heading,body,note_date,created_at,updated_at FROM study_notes", r =>
            {
                Required(r.GetString(1)); _ = r.GetString(2); ParseDate(r.GetString(3)); ParseTime(r.GetString(4)); ParseTime(r.GetString(5));
            });
            CheckRows(cmd, "SELECT key,value FROM preferences", r => { _ = r.GetString(0); _ = r.GetString(1); });
        }
        return version;
    }
    private static void Required(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ValidationException("This database contains a missing heading or name.");
    }
    private static void ParseTime(string value) => _ = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    private static void ParseDate(string value) => _ = DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static void CheckRows(SqliteCommand cmd, string query, Action<SqliteDataReader> check)
    {
        cmd.CommandText = query;
        using var r = cmd.ExecuteReader();
        while (r.Read()) check(r);
    }
    internal static void Copy(string source, string destination)
    {
        using var input = Open(source, true);
        using var output = Open(destination);
        input.BackupDatabase(output);
    }
    public static void Export(string source, string destination)
    {
        destination = Path.GetFullPath(destination);
        if (File.Exists(destination)) throw new ValidationException("Please choose a new backup filename so an existing database is kept.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var pending = destination + "." + Guid.NewGuid().ToString("N") + ".pending";
        try
        {
            Copy(source, pending);
            Validate(pending);
            File.Move(pending, destination);
        }
        finally { DeleteStaging(pending); }
    }
    internal static void DeleteStaging(string path)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            if (File.Exists(path + suffix)) File.Delete(path + suffix);
    }
    // Recovery deliberately retains the backup's schema; launch a compatible release afterwards.
    public static void Recover(string backup, string database)
    {
        var version = Validate(backup);
        Copy(backup, database);
        if (Validate(database) != version) throw new ValidationException("Recovery verification failed. Keep the original backup and contact support.");
    }
}

public sealed class AppDataPaths(string localAppData)
{
    public string DatabasePath { get; } = Path.Combine(localAppData, "CADesk", "Data", "cadesk.db");
    public string BackupDirectory { get; } = Path.Combine(localAppData, "CADesk", "Backups");
    public string LegacyPath { get; } = Path.Combine(localAppData, "CA Desk", "ca-desk.db");
    public static AppDataPaths Current => new(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    public Repository OpenRepository()
    {
        if (!File.Exists(DatabasePath) && File.Exists(LegacyPath))
        {
            DatabaseSafety.Validate(LegacyPath);
            Directory.CreateDirectory(BackupDirectory);
            DatabaseSafety.Export(LegacyPath, Path.Combine(BackupDirectory, $"cadesk-legacy-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db"));
            DatabaseSafety.Export(LegacyPath, DatabasePath);
        }
        return new Repository(DatabasePath, BackupDirectory);
    }
}
