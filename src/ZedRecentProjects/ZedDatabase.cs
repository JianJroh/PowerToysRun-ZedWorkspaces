using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

namespace ZedRecentProjects;

/// <summary>
/// Reads the recent project list from Zed's SQLite workspace database.
/// </summary>
public static class ZedDatabase
{
    private static bool _sqliteReady;

    /// <summary>
    /// Returns recent local directory projects, newest first, deduplicated by root path.
    /// Never throws: any read problem results in an empty list.
    /// </summary>
    public static List<ZedProject> LoadRecentProjects(int max)
    {
        try
        {
            EnsureSqliteReady();
            var dbPath = FindDatabase();
            if (dbPath == null)
            {
                return new List<ZedProject>();
            }

            using var connection = OpenReadOnly(dbPath);
            return QueryProjects(connection, max);
        }
        catch (Exception)
        {
            return new List<ZedProject>();
        }
    }

    private static string FindDatabase()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbRoot = Path.Combine(local, "Zed", "db");
        if (!Directory.Exists(dbRoot))
        {
            return null;
        }

        // Prefer the stable channel; fall back to any other channel (e.g. 0-preview).
        var stable = Path.Combine(dbRoot, "0-stable", "db.sqlite");
        if (File.Exists(stable))
        {
            return stable;
        }

        return Directory.GetDirectories(dbRoot, "0-*")
            .OrderByDescending(d => d)
            .Select(d => Path.Combine(d, "db.sqlite"))
            .FirstOrDefault(File.Exists);
    }

    private static SqliteConnection OpenReadOnly(string dbPath)
    {
        var builder = new SqliteConnectionStringBuilder($"Data Source={dbPath};Mode=ReadOnly;Cache=Shared");
        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    private static List<ZedProject> QueryProjects(SqliteConnection connection, int limit)
    {
        var results = new List<ZedProject>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT paths, timestamp FROM workspaces "
            + "WHERE paths IS NOT NULL AND paths != '' "
            + "AND (remote_connection_id IS NULL OR remote_connection_id = '') "
            + "ORDER BY timestamp DESC LIMIT @limit";
        command.Parameters.AddWithValue("@limit", Math.Max(1, Math.Min(limit, 2000)));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var raw = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var paths = raw
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(p => Directory.Exists(p)) // keep only real local directories
                .ToArray();
            if (paths.Length == 0 || !seenPaths.Add(paths[0]))
            {
                continue;
            }

            DateTime? openedAtUtc = null;
            if (!reader.IsDBNull(1))
            {
                var rawTimestamp = reader.GetString(1);
                if (DateTime.TryParse(
                        rawTimestamp,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                        out var parsed))
                {
                    openedAtUtc = parsed;
                }
            }

            results.Add(new ZedProject(paths, openedAtUtc));
        }

        return results;
    }

    private static void EnsureSqliteReady()
    {
        if (_sqliteReady)
        {
            return;
        }

        _sqliteReady = true;

        // SQLitePCLRaw loads e_sqlite3 by name from the process search paths, which do not
        // include the plugin folder. Pre-load it by absolute path so the provider can resolve it.
        var assemblyDir = Path.GetDirectoryName(typeof(ZedDatabase).Assembly.Location);
        var native = Path.Combine(assemblyDir ?? string.Empty, "e_sqlite3.dll");
        if (File.Exists(native))
        {
            try
            {
                NativeLibrary.Load(native);
            }
            catch (Exception)
            {
                // The provider may still be able to resolve the library on its own.
            }
        }
    }
}