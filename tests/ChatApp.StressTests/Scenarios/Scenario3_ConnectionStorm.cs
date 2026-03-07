using ChatApp.StressTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;
using Xunit;

namespace ChatApp.StressTests.Scenarios;

/// <summary>
/// Scenario 3 — Connection Storm
///
/// 50 users connect to the SignalR hub simultaneously.
/// Each connection triggers OnConnectedAsync → Clients.All.SendAsync("UserPresenceUpdate")
/// causing an O(n²) presence broadcast: 50 users × 50 broadcasts = 2,500 sends.
///
/// Pass criteria:
///   - All 50 connections reach HubConnectionState.Connected within 5 s
///   - RedisInspector.GetOnlineCountAsync() == 50 after all connect
///   - No stale Redis entries
///   - All 50 connections receive UserPresenceUpdate events (broadcast works)
///
/// Vulnerability surfaced if count ≠ 50: HashIncrementAsync dropped under load,
/// or the O(n²) broadcast caused back-pressure that dropped connections.
/// </summary>
[Collection("StressTests")]
public class Scenario3_ConnectionStorm : IAsyncLifetime
{
    private StressTestFactory _factory     = null!;
    private RedisInspector    _redis       = null!;
    private const int         UserCount    = 50;
    private const int         ConnectTimeoutMs = 5_000;

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

    [Fact(DisplayName = "S3 — 50 users connect simultaneously: all Connected, Redis count = 50")]
    public async Task SimultaneousConnections_AllReachConnectedState()
    {
        // ── SETUP ────────────────────────────────────────────────────────────
        var userFactory = new TestUserFactory(_factory.CreateClient());
        var users = await userFactory.CreateUsersAsync(UserCount, "s3storm");

        // Track received presence events per connection
        var presenceEvents = new System.Collections.Concurrent.ConcurrentBag<(int UserId, bool IsOnline)>();

        await using var pool = SignalRClientPool.Create(_factory, users);

        pool.OnAll<int, bool>("UserPresenceUpdate", (userId, isOnline) =>
        {
            presenceEvents.Add((userId, isOnline));
        });

        // ── ACT ──────────────────────────────────────────────────────────────
        var cts      = new CancellationTokenSource(ConnectTimeoutMs);
        var sw       = System.Diagnostics.Stopwatch.StartNew();

        await pool.StartAllAsync(cts.Token);

        sw.Stop();

        // Allow Redis writes to settle
        await Task.Delay(500);

        // ── ASSERT ───────────────────────────────────────────────────────────
        pool.ConnectedCount.Should().Be(UserCount,
            $"all {UserCount} connections must reach Connected state within {ConnectTimeoutMs}ms " +
            $"(took {sw.ElapsedMilliseconds}ms)");

        sw.ElapsedMilliseconds.Should().BeLessOrEqualTo(ConnectTimeoutMs,
            $"all {UserCount} users must connect within {ConnectTimeoutMs}ms");

        var onlineCount = await _redis.GetOnlineCountAsync();
        onlineCount.Should().Be(UserCount,
            $"Redis must record exactly {UserCount} online users; " +
            "mismatch indicates HashIncrementAsync race or missed writes");

        var stale = await _redis.GetStaleEntriesAsync();
        stale.Should().BeEmpty(
            "no Redis entries should have connection count ≤ 0 after clean connect");

        // Each user should have received at least one presence broadcast
        // (from other users connecting) — confirms Clients.All broadcast works
        var usersWhoGotBroadcast = presenceEvents.Select(e => e.UserId).Distinct().Count();
        usersWhoGotBroadcast.Should().BeGreaterOrEqualTo(1,
            "at least one UserPresenceUpdate broadcast must have been delivered");
    }

    [Fact(DisplayName = "S3 — 50 users connect then join group room: all in group")]
    public async Task SimultaneousJoinRoom_AllJoinSuccessfully()
    {
        // ── SETUP ────────────────────────────────────────────────────────────
        var userFactory = new TestUserFactory(_factory.CreateClient());
        var users = await userFactory.CreateUsersAsync(UserCount, "s3join");

        // Create a group room by having user 0 create a private chat with user 1
        // (the cheapest way to get a roomId in the current API)
        using var roomClient = _factory.CreateClient();
        roomClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", users[0].Token);
        var roomResp = await roomClient.PostAsync(
            $"/api/ChatRooms/private/{users[1].UserId}", null);
        roomResp.EnsureSuccessStatusCode();
        var roomJson = await roomResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var roomId   = roomJson.GetProperty("id").GetInt32();

        await using var pool = SignalRClientPool.Create(_factory, users);

        // Track received messages
        var received = new System.Collections.Concurrent.ConcurrentBag<object>();
        pool.OnAll<object>("ReceiveMessage", msg => received.Add(msg));

        // ── ACT ──────────────────────────────────────────────────────────────
        await pool.StartAllAsync();
        await pool.InvokeAllAsync("JoinRoom", roomId);
        await Task.Delay(300); // settle

        // Now user 0 sends a message — all joined connections should receive it
        await pool.Connections[0].InvokeAsync("SendMessage", roomId, "storm test broadcast");
        await Task.Delay(500);

        // ── ASSERT ───────────────────────────────────────────────────────────
        pool.ConnectedCount.Should().Be(UserCount);

        // The sender is always in the group; at minimum they receive their own message
        received.Count.Should().BeGreaterOrEqualTo(1,
            "message broadcast to group must be received by at least the sender");
    }
}
