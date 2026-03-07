using ChatApp.StressTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ChatApp.StressTests.Scenarios;

/// <summary>
/// Scenario 5 — Message Burst (Spike Test)
///
/// 50 users all fire 20 messages at exactly the same time (no delay).
/// Total: 1,000 messages sent in a single burst.
///
/// Pass criteria:
///   - 0 send errors
///   - All 50 users receive all 1,000 messages (50,000 total deliveries)
///   - DB contains exactly 1,000 messages
///   - Entire burst completes in ≤ 5,000 ms
///
/// This test catches:
///   - DB write contention (MessageRepository.CreateAsync has no transactions)
///   - SignalR group broadcast dropping under instantaneous high fan-out
///   - Npgsql pool exhaustion (default MaxPoolSize = 20, we use 50 users)
/// </summary>
[Collection("StressTests")]
public class Scenario5_MessageBurst : IAsyncLifetime
{
    private StressTestFactory _factory = null!;
    private DbInspector       _db      = null!;

    private const int UserCount    = 50;
    private const int MsgsPerUser  = 20;
    private const int TotalMsgs    = UserCount * MsgsPerUser; // 1,000

    public async Task InitializeAsync()
    {
        _factory = new StressTestFactory();
        await _factory.ResetDatabaseAsync();
        _db = new DbInspector();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact(DisplayName = "S5 — 50 users × 20 msgs burst: 1,000 msgs in ≤ 5s, zero drops")]
    public async Task MessageBurst_ZeroDropsWithinDeadline()
    {
        // ── SETUP ────────────────────────────────────────────────────────────
        var userFactory = new TestUserFactory(_factory.CreateClient());
        var users = await userFactory.CreateUsersAsync(UserCount, "s5burst");

        // Create shared room for all 50 users
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", users[0].Token);

        var roomResp = await adminClient.PostAsync(
            $"/api/ChatRooms/private/{users[1].UserId}", null);
        roomResp.EnsureSuccessStatusCode();
        var roomJson = await roomResp.Content.ReadFromJsonAsync<JsonElement>();
        var roomId   = roomJson.GetProperty("id").GetInt32();

        // Add remaining users to room via DbContext
        using var scope = _factory.Services.CreateScope();
        var       dbCtx = scope.ServiceProvider
            .GetService(typeof(ChatApp.Infrastructure.Data.ChatDbContext))
            as ChatApp.Infrastructure.Data.ChatDbContext;

        for (var i = 2; i < users.Count; i++)
        {
            dbCtx!.RoomParticipants.Add(new ChatApp.Core.Entities.RoomParticipant
            {
                RoomId = roomId,
                UserId = users[i].UserId
            });
        }
        await dbCtx!.SaveChangesAsync();

        // ── SIGNALR POOL ─────────────────────────────────────────────────────
        var received = new ConcurrentDictionary<int, int>();
        foreach (var u in users) received[u.UserId] = 0;

        await using var pool = SignalRClientPool.Create(_factory, users);

        for (var i = 0; i < pool.Connections.Count; i++)
        {
            var userId = users[i].UserId;
            pool.Connections[i].On<JsonElement>("ReceiveMessage", _ =>
            {
                received.AddOrUpdate(userId, 1, (_, old) => old + 1);
            });
        }

        await pool.StartAllAsync();
        await pool.InvokeAllAsync("JoinRoom", roomId);
        await Task.Delay(300);

        // ── ACT: full burst ───────────────────────────────────────────────────
        var sendErrors = new ConcurrentBag<string>();
        var sw         = System.Diagnostics.Stopwatch.StartNew();

        var sendTasks = pool.Connections.SelectMany((conn, i) =>
            Enumerable.Range(0, MsgsPerUser).Select(j =>
                conn.InvokeAsync("SendMessage", roomId,
                    $"[U{users[i].UserId}|m{j}] burst")
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            sendErrors.Add($"U{users[i].UserId}-m{j}: {t.Exception?.Message}");
                    })
            )
        );

        await Task.WhenAll(sendTasks);
        var sendElapsedMs = sw.ElapsedMilliseconds;

        // Allow delivery to propagate to all clients
        await Task.Delay(4000);
        sw.Stop();

        // ── ASSERT ───────────────────────────────────────────────────────────
        sendErrors.Should().BeEmpty(
            $"all {TotalMsgs} SendMessage invocations must succeed; " +
            $"errors: {string.Join("; ", sendErrors.Take(5))}");

        sendElapsedMs.Should().BeLessOrEqualTo(5000,
            $"burst of {TotalMsgs} messages must complete within 5,000ms " +
            $"(actual: {sendElapsedMs}ms)");

        // DB ground truth
        var dbCount = await _db.CountMessagesInRoomAsync(roomId);
        dbCount.Should().Be(TotalMsgs,
            $"DB must persist all {TotalMsgs} messages — " +
            "shortfall indicates concurrent write contention or pool exhaustion");

        // Delivery completeness
        var drops = received
            .Where(kv => kv.Value < TotalMsgs)
            .Select(kv => $"U{kv.Key}: {kv.Value}/{TotalMsgs}")
            .ToList();

        drops.Should().BeEmpty(
            $"every user must receive all {TotalMsgs} messages; " +
            $"users with drops: {string.Join(", ", drops)}");
    }
}
