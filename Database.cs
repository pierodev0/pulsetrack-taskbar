using Microsoft.Data.Sqlite;

namespace PulseTrack.Taskbar;

public sealed class Database : ISessionRepository
{
    private readonly SqliteConnection _conn;

    public Database(string? directory = null)
    {
        var dir = directory ?? FileLogger.DefaultDirectory();
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "sessions.db");

        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA busy_timeout = 5000;
            CREATE TABLE IF NOT EXISTS app_sessions (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              app_name TEXT,
              start_time TEXT NOT NULL,
              end_time TEXT,
              duration_seconds REAL NOT NULL DEFAULT 0,
              status TEXT NOT NULL DEFAULT 'active',
              created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS time_blocks (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              session_id INTEGER NOT NULL REFERENCES app_sessions(id),
              app_name TEXT,
              label TEXT NOT NULL,
              start_time TEXT NOT NULL,
              end_time TEXT,
              duration_seconds REAL NOT NULL DEFAULT 0,
              status TEXT NOT NULL DEFAULT 'active'
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public long CreateSession(string? appName, string startTime)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO app_sessions (app_name, start_time, duration_seconds, status, end_time) VALUES ($app, $start, 0, 'active', NULL); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$app", (object?)appName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$start", startTime);
        return (long)cmd.ExecuteScalar()!;
    }

    public void CloseSession(long id, string endTime, double durationSeconds)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE app_sessions SET end_time = $end, duration_seconds = $dur, status = 'closed' WHERE id = $id";
        cmd.Parameters.AddWithValue("$end", endTime);
        cmd.Parameters.AddWithValue("$dur", durationSeconds);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void UpdateSessionDuration(long id, double durationSeconds)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE app_sessions SET duration_seconds = $dur WHERE id = $id";
        cmd.Parameters.AddWithValue("$dur", durationSeconds);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public long CreateBlock(long sessionId, string? appName, string label, string startTime)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO time_blocks (session_id, app_name, label, start_time, duration_seconds, status, end_time) VALUES ($sid, $app, $label, $start, 0, 'active', NULL); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$sid", sessionId);
        cmd.Parameters.AddWithValue("$app", (object?)appName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$label", label);
        cmd.Parameters.AddWithValue("$start", startTime);
        return (long)cmd.ExecuteScalar()!;
    }

    public void CloseBlock(long id, string endTime, double durationSeconds)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE time_blocks SET end_time = $end, duration_seconds = $dur, status = 'closed' WHERE id = $id";
        cmd.Parameters.AddWithValue("$end", endTime);
        cmd.Parameters.AddWithValue("$dur", durationSeconds);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void UpdateBlockDuration(long id, double durationSeconds)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE time_blocks SET duration_seconds = $dur WHERE id = $id";
        cmd.Parameters.AddWithValue("$dur", durationSeconds);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void CloseStaleActive()
    {
        var now = DateTime.UtcNow.ToString("o");
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE time_blocks SET end_time = $now, status = 'closed' WHERE status = 'active'; " +
            "UPDATE app_sessions SET end_time = $now, status = 'closed' WHERE status = 'active'";
        cmd.Parameters.AddWithValue("$now", now);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
