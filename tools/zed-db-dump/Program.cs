using System.Text;
using Microsoft.Data.Sqlite;

var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Zed", "db", "0-stable", "db.sqlite");
var csb = new SqliteConnectionStringBuilder("Data Source=" + dbPath + ";Mode=ReadOnly;Cache=Shared");
using var conn = new SqliteConnection(csb.ConnectionString);
conn.Open();

Console.WriteLine("== current schema of workspaces ==");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "PRAGMA table_info(workspaces)";
    using var r = cmd.ExecuteReader();
    while (r.Read())
        Console.WriteLine($"  {r.GetValue(1)} ({r.GetValue(2)}) notnull={r.GetValue(3)} pk={r.GetValue(5)}");
}

Console.WriteLine("\n== row count ==");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "SELECT COUNT(*) FROM workspaces";
    Console.WriteLine("  " + cmd.ExecuteScalar());
}

Console.WriteLine("\n== sample rows (all columns) ==");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "SELECT workspace_id, paths, paths_order, remote_connection_id, timestamp, identity_paths FROM workspaces ORDER BY timestamp DESC LIMIT 10";
    using var r = cmd.ExecuteReader();
    int n = r.FieldCount;
    while (r.Read())
    {
        Console.WriteLine("--- row ---");
        for (int i = 0; i < n; i++)
        {
            var v = r.GetValue(i);
            var name = r.GetName(i);
            string shown;
            if (v is byte[] b)
            {
                var text = Encoding.UTF8.GetString(b).Replace("\0", "\\0");
                shown = $"blob[{b.Length}] hex={Convert.ToHexString(b).Substring(0, Math.Min(60, b.Length*2))} text={text}";
            }
            else shown = v?.ToString() ?? "NULL";
            Console.WriteLine($"  {name} = {shown}");
        }
    }
}

Console.WriteLine("\n== rows with real paths ==");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "SELECT workspace_id, paths, timestamp, remote_connection_id FROM workspaces WHERE paths IS NOT NULL AND paths != '' ORDER BY timestamp DESC LIMIT 10";
    using var r = cmd.ExecuteReader();
    while (r.Read())
        Console.WriteLine($"  id={r.GetValue(0)} ts={r.GetValue(2)} remote={r.GetValue(3)} paths='{r.GetValue(1)}'");
}