using ChatApp.StressTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace ChatApp.StressTests.Scenarios;

/// <summary>
/// Scenario 6 — Reconnection Storm
///
/// 30 users connect, then all disconnect simultaneously, then all reconnect.
/// This exercises the PresenceService race in UserDisconnectedAsync:
///
///   var count = await _redis.HashIncrementAsync(key, userId, -1);  // Step A
///   if (count &lt;= 0)
///       await _redis.HashDeleteAsync(key, userId);                  // Step B
///
/// Steps A and B are not atomic. Two concurrent disconnects for the same user
/// (multiple tabs) can both read count=1, both decrement to 0, and the second
/// HashDeleteAsync hits a missing key — harmless but the entry can also be
/// left with a negative count (stale entry).
///
/// Pass criteria:
///   - After reconnect: Redis online count == 30
///   - GetStaleEntriesAsync() is empty (no entries with count ≤ 0)
///   - All 30 connections reach Connected state after reconnect
///
/// Failure means: PresenceService needs an atomic Lua decrement+delete.
/// </summary>
[Collection("StressTests")]
public class Scenario6_ReconnectionStorm : IAsyncLifetime
{
    private StressTestFactory _factory   = null!;
    private RedisInspector    _redis     = null!;
    private const int         UserCount  = 30;

    public async Task InitializeAsync()
    {
        _factory = new StressTestFactory();
        await _factory.ResetDatabaseAsync();
        _redis = new RedisInspector();
        await _redis.ClearPresenceAsync();
    }

    public Task DisposeAsync()
    {
        _redis.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact(DisplayName = "S6 — 30 users disconnect+reconnect simultaneously: Redis count stays at 30")]
    public async Task SimultaneousReconnect_PresenceCountCorrect()
    {
        // ── SETUP ────────────────────────────────────────────────────────────
        var userFactory = new TestUserFactory(_factory.CreateClient());
        var users = await userFactory.CreateUsersAsync(UserCount, "s6reconn");

        await using var pool = SignalRClientPool.Create(_factory, users);

        // Phase 1: connect all
        await pool.StartAllAsync();
        await Task.Delay(500);

        var initialCount = await _redis.GetOnlineCountAsync();
        initialCount.Should().Be(UserCount,
            "all users must be online in Redis before the reconnect test");

        // ── ACT: simultaneous disconnect then reconnect ───────────────────────
        // Stop all connections at once — this is the storm
        await pool.StopAllAsync();
        await Task.Delay(500); // let OnDisconnectedAsync flush

        var afterDisconnectCount = await _redis.GetOnlineCountAsync();

        // Reconnect all simultaneously
        await pool.StartAllAsync();
        await Task.Delay(500);

        // ── ASSERT ───────────────────────────────────────────────────────────
        afterDisconnectCount.Should().Be(0,
            "Redis must show 0 online users immediately after all disconnect");

        pool.ConnectedCount.Should().Be(UserCount,
            $"all {UserCount} connections must reach Connected state after reconnect");

        var afterReconnectCount = await _redis.GetOnlineCountAsync();
        afterReconnectCount.Should().Be(UserCount,
            $"Redis must show exactly {UserCount} online users after reconnect; " +
            "mismatch indicates the HashIncrementAsync / HashDeleteAsync race fired " +
            "and left stale or missing presence entries");

        var stale = await _redis.GetStaleEntriesAsync();
        stale.Should().BeEmpty(
            "no Redis presence entries should have count ≤ 0 after reconnect; " +
            "stale entries indicate the non-atomic decrement+delete race in PresenceService");
    }

    [Fact(DisplayName = "S6 — Single user opens 3 tabs then closes all: Redis entry removed cleanly")]
    public async Task MultiTabDisconnect_NoStaleEntry()
    {
        // Tests the reference-counting logic in PresenceService:
        //   UserConnectedAsync increments per connectionId
        //   UserDisconnectedAsync decrements; deletes when count reaches 0
        // With 3 simultaneous disconnects the non-atomic delete can race.

        var userFactory = new TestUserFactory(_factory.CreateClient());
        var user = (await userFactory.CreateUsersAsync(1, "s6tabs"))[0];

        // Simulate 3 concurrent tab connections for the same user
        var connections = Enumerable.Range(0, 3).Select(_ =>
            new HubConnectionBuilder()
                .WithUrl(new Uri(_factory.Server.BaseAddress, "chatHub"),
                    opts =>
                    {
                        opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                        opts.AccessTokenProvider = () => Task.FromResult<string?>(user.Token);
                    })
                .Build()
        ).ToList();

        await Task.WhenAll(connections.Select(c => c.StartAsync()));
        await Task.Delay(300);

        var onlineDuring = await _redis.GetOnlineCountAsync();
        onlineDuring.Should().Be(1, "user appears online once regardless of tab count");

        var rawDuring = await _redis.GetRawPresenceAsync();
        rawDuring[user.UserId.ToString()].Should().Be(3,
            "connection count in Redis must be 3 (one per tab)");

        // Close all 3 tabs simultaneously
        await Task.WhenAll(connections.Select(c => c.StopAsync()));
        await Task.Delay(500);

        var onlineAfter = await _redis.GetOnlineCountAsync();
        onlineAfter.Should().Be(0,
            "user must be offline after all tabs close");

        var stale = await _redis.GetStaleEntriesAsync();
        stale.Should().BeEmpty(
            "no stale Redis entries should remain after all tabs close cleanly");

        foreach (var c in connections) await c.DisposeAsync();
    }
}
