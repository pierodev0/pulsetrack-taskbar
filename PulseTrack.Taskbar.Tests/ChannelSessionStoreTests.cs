using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class ChannelSessionStoreTests
{
    private static ChannelSessionStore CreateStore(FakeSessionRepository inner) =>
        new(inner);

    [Fact]
    public async Task WritesExecuteInBackgroundAndReturnIds()
    {
        var inner = new FakeSessionRepository();
        await using var store = CreateStore(inner);

        var sid = await store.CreateSessionAsync("Code", "2026-09-04T12:00:00Z");
        var bid = await store.CreateBlockAsync(sid, "Code", "Lap 1", "2026-09-04T12:00:00Z");

        Assert.Equal(1, sid);
        Assert.Equal(1, bid);
        Assert.Single(inner.Sessions);
        Assert.Single(inner.Blocks);
    }

    [Fact]
    public async Task ConcurrentEnqueuesPreserveFifoOrder()
    {
        var inner = new FakeSessionRepository();
        await using var store = CreateStore(inner);

        var t1 = store.CreateSessionAsync("A", "t1");
        var t2 = store.CreateSessionAsync("B", "t2");
        var ids = await Task.WhenAll(t1, t2);

        Assert.Equal(new[] { 1L, 2L }, ids);
        Assert.Equal("A", inner.Sessions[0].App);
        Assert.Equal("B", inner.Sessions[1].App);
    }

    [Fact]
    public async Task DisposeAsyncDrainsPendingWrites()
    {
        var inner = new FakeSessionRepository();
        var store = CreateStore(inner);

        var sid = await store.CreateSessionAsync("Code", "t0");
        _ = store.UpdateSessionDurationAsync(sid, 42.0);
        _ = store.CloseSessionAsync(sid, "t1", 42.0);

        await store.DisposeAsync();

        var closed = inner.Sessions.Single();
        Assert.Equal("closed", closed.Status);
        Assert.Equal(42.0, closed.Duration, precision: 5);
    }

    [Fact]
    public async Task CloseBlockAndSessionFlow()
    {
        var inner = new FakeSessionRepository();
        await using var store = CreateStore(inner);

        var sid = await store.CreateSessionAsync("Code", "t0");
        var bid = await store.CreateBlockAsync(sid, "Code", "Lap 1", "t0");
        await store.CloseBlockAsync(bid, "t1", 2.0);
        await store.CloseSessionAsync(sid, "t1", 2.0);

        Assert.Equal("closed", inner.Sessions[0].Status);
        Assert.Equal("closed", inner.Blocks[0].Status);
        Assert.Equal(2.0, inner.Blocks[0].Duration, precision: 5);
    }
}
