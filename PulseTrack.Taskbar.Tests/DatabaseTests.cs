using Microsoft.Data.Sqlite;
using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class DatabaseTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"pt-db-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private List<(string? App, string Status)> Sessions()
    {
        using var conn = new SqliteConnection($"Data Source={Path.Combine(_dir, "sessions.db")}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT app_name, status FROM app_sessions ORDER BY id";
        using var reader = cmd.ExecuteReader();
        var rows = new List<(string?, string)>();
        while (reader.Read())
            rows.Add((reader.IsDBNull(0) ? null : reader.GetString(0), reader.GetString(1)));
        return rows;
    }

    private List<(string? App, string Label, string Status)> Blocks()
    {
        using var conn = new SqliteConnection($"Data Source={Path.Combine(_dir, "sessions.db")}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT app_name, label, status FROM time_blocks ORDER BY id";
        using var reader = cmd.ExecuteReader();
        var rows = new List<(string?, string, string)>();
        while (reader.Read())
            rows.Add((reader.IsDBNull(0) ? null : reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return rows;
    }

    [Fact]
    public void CreatesItsOwnDatabaseInTheGivenDirectory()
    {
        using var db = new Database(_dir);

        Assert.True(File.Exists(Path.Combine(_dir, "sessions.db")));
    }

    [Fact]
    public void NullAppName_IsStoredAsNull_NotAsEmptyString()
    {
        using var db = new Database(_dir);

        var sid = db.CreateSession(null, "t0");
        _ = db.CreateBlock(sid, null, "Lap 1", "t0");

        Assert.Null(Sessions()[0].App);
        Assert.Null(Blocks()[0].App);
    }

    [Fact]
    public void AppName_RoundTrips()
    {
        using var db = new Database(_dir);

        var sid = db.CreateSession("Code", "t0");
        _ = db.CreateBlock(sid, "Code", "Lap 1", "t0");

        Assert.Equal("Code", Sessions()[0].App);
        Assert.Equal("Code", Blocks()[0].App);
    }

    [Fact]
    public void CloseSessionAndBlock_MarkThemClosed()
    {
        using var db = new Database(_dir);
        var sid = db.CreateSession(null, "t0");
        var bid = db.CreateBlock(sid, null, "Lap 1", "t0");

        db.CloseBlock(bid, "t1", 2.0);
        db.CloseSession(sid, "t1", 2.0);

        Assert.Equal("closed", Sessions()[0].Status);
        Assert.Equal("closed", Blocks()[0].Status);
    }

    [Fact]
    public void CloseStaleActive_ClosesEveryOpenRow()
    {
        using var db = new Database(_dir);
        var sid = db.CreateSession("Code", "t0");
        _ = db.CreateBlock(sid, "Code", "Lap 1", "t0");

        db.CloseStaleActive();

        Assert.Equal("closed", Sessions()[0].Status);
        Assert.Equal("closed", Blocks()[0].Status);
    }

    [Fact]
    public void MultipleBlocks_PreserveInsertOrderAndLabels()
    {
        using var db = new Database(_dir);
        var sid = db.CreateSession(null, "t0");
        _ = db.CreateBlock(sid, null, "Lap 1", "t0");
        _ = db.CreateBlock(sid, null, "Lap 2", "t1");

        var blocks = Blocks();

        Assert.Equal(2, blocks.Count);
        Assert.Equal("Lap 1", blocks[0].Label);
        Assert.Equal("Lap 2", blocks[1].Label);
    }

    [Fact]
    public void ReopeningTheSameDirectory_KeepsExistingData()
    {
        using (var first = new Database(_dir))
            _ = first.CreateSession(null, "t0");

        using var second = new Database(_dir);
        _ = second.CreateSession("Code", "t1");

        var sessions = Sessions();
        Assert.Equal(2, sessions.Count);
        Assert.Null(sessions[0].App);
        Assert.Equal("Code", sessions[1].App);
    }
}
