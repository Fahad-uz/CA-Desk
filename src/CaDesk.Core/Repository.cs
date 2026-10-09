using Microsoft.Data.Sqlite;
using System.Globalization;
namespace CaDesk.Core;

public sealed class ValidationException(string message) : Exception(message);
public sealed class Repository
{
    private readonly string connectionString;
    public string DatabasePath { get; }
    public Repository(string path)
    {
        DatabasePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource=DatabasePath, ForeignKeys=true, DefaultTimeout=5, Pooling=false }.ToString();
        using var db=Open();
        using var version=db.CreateCommand(); version.CommandText="PRAGMA user_version";
        var current=Convert.ToInt32(version.ExecuteScalar());
        if(current>1) throw new ValidationException("This database was created by a newer CA Desk version. Please update CA Desk.");
        if(current==0)
        {
            using var tx=db.BeginTransaction();
            using var cmd=db.CreateCommand();cmd.Transaction=tx;
            cmd.CommandText="""
                CREATE TABLE clients(id INTEGER PRIMARY KEY, name TEXT NOT NULL CHECK(length(trim(name))>0), normalized_name TEXT NOT NULL UNIQUE, description TEXT NOT NULL DEFAULT '', created_at TEXT NOT NULL);
                CREATE TABLE work_areas(id INTEGER PRIMARY KEY, client_id INTEGER NOT NULL REFERENCES clients(id) ON DELETE CASCADE, name TEXT NOT NULL CHECK(length(trim(name))>0), status INTEGER NOT NULL DEFAULT 0 CHECK(status IN(0,1,2)), deadline TEXT, notes TEXT NOT NULL DEFAULT '', created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
                CREATE INDEX ix_work_client ON work_areas(client_id);
                CREATE INDEX ix_work_deadline ON work_areas(deadline);
                PRAGMA user_version=1;
                """;
            cmd.ExecuteNonQuery();tx.Commit();
        }
        using var wal=db.CreateCommand();wal.CommandText="PRAGMA journal_mode=WAL";wal.ExecuteScalar();
    }
    private SqliteConnection Open() { var db=new SqliteConnection(connectionString);db.Open();return db; }
    private static string Timestamp()=>DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture);
    private static DateTime ParseTime(string text)=>DateTime.Parse(text,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind);
    public Snapshot Load()
    {
        using var db=Open(); using var tx=db.BeginTransaction();
        var clients=new List<Client>();var works=new List<WorkArea>();
        using(var cmd=db.CreateCommand())
        {
            cmd.Transaction=tx;cmd.CommandText="SELECT id,name,description,created_at FROM clients ORDER BY name COLLATE NOCASE,id";
            using var r=cmd.ExecuteReader();while(r.Read()) clients.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),ParseTime(r.GetString(3))));
        }
        using(var cmd=db.CreateCommand())
        {
            cmd.Transaction=tx;cmd.CommandText="SELECT id,client_id,name,status,deadline,notes,created_at,updated_at FROM work_areas";
            using var r=cmd.ExecuteReader();while(r.Read()) works.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetString(2),(WorkStatus)r.GetInt32(3),r.IsDBNull(4)?null:DateOnly.ParseExact(r.GetString(4),"yyyy-MM-dd",CultureInfo.InvariantCulture),r.GetString(5),ParseTime(r.GetString(6)),ParseTime(r.GetString(7))));
        }
        tx.Commit();return new(clients,works);
    }
    public long SaveClient(long? id,string name,string? description)
    {
        name=name.Trim();if(name.Length==0) throw new ValidationException("Please enter a client name.");
        using var db=Open();using var cmd=db.CreateCommand();
        cmd.CommandText=id is null ? "INSERT INTO clients(name,normalized_name,description,created_at) VALUES($name,$normal,$description,$created) RETURNING id" : "UPDATE clients SET name=$name, normalized_name=$normal, description=$description WHERE id=$id RETURNING id";
        cmd.Parameters.AddWithValue("$id",(object?)id??DBNull.Value);cmd.Parameters.AddWithValue("$name",name);cmd.Parameters.AddWithValue("$normal",name.ToUpperInvariant());cmd.Parameters.AddWithValue("$description",description??"");cmd.Parameters.AddWithValue("$created",Timestamp());
        try { return Convert.ToInt64(cmd.ExecuteScalar()??throw new ValidationException("This client no longer exists. Please refresh and try again.")); }
        catch(SqliteException ex) when(ex.SqliteExtendedErrorCode==2067) { throw new ValidationException("A client with this name already exists. Please choose a different name."); }
    }
    public long SaveWork(long? id,long clientId,string name,WorkStatus status=WorkStatus.NotStarted,DateOnly? deadline=null,string? notes=null)
    {
        name=name.Trim();if(name.Length==0)throw new ValidationException("Please enter a work area name.");
        if(!Enum.IsDefined(status))throw new ValidationException("Please choose one of the three work statuses.");
        using var db=Open();using var cmd=db.CreateCommand();
        cmd.CommandText=id is null ? "INSERT INTO work_areas(client_id,name,status,deadline,notes,created_at,updated_at) VALUES($client,$name,$status,$deadline,$notes,$now,$now) RETURNING id" : "UPDATE work_areas SET client_id=$client,name=$name,status=$status,deadline=$deadline,notes=$notes,updated_at=$now WHERE id=$id RETURNING id";
        cmd.Parameters.AddWithValue("$id",(object?)id??DBNull.Value);cmd.Parameters.AddWithValue("$client",clientId);cmd.Parameters.AddWithValue("$name",name);cmd.Parameters.AddWithValue("$status",(int)status);cmd.Parameters.AddWithValue("$deadline",(object?)deadline?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)??DBNull.Value);cmd.Parameters.AddWithValue("$notes",notes??"");cmd.Parameters.AddWithValue("$now",Timestamp());
        try { return Convert.ToInt64(cmd.ExecuteScalar()??throw new ValidationException("This work area no longer exists. Please refresh and try again.")); }
        catch(SqliteException ex) when(ex.SqliteExtendedErrorCode==787) { throw new ValidationException("Please choose an existing client."); }
    }
    public void DeleteClient(long id)=>Delete("clients",id);
    public void DeleteWork(long id)=>Delete("work_areas",id);
    private void Delete(string table,long id) { using var db=Open();using var cmd=db.CreateCommand();cmd.CommandText=$"DELETE FROM {table} WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);cmd.ExecuteNonQuery(); }
}
