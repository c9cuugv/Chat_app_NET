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
/// Scenario 4 — Sustained Group Messaging (60 seconds)
///
/// 50 users share one group room. Each sends 1 message/second for 60 seconds
/// → 3,000 messages total. Every user must receive every message.
///
/// Pass criteria:
///   - 0 send errors
///   - Every user receives exactly 3,000 messages (zero drops)
///   - DB contains exactly 3,000 messages in the room
///   - No SignalR reconnections triggered
///
/// Vulnerability surfaced: any delivery gap under sustained load from
/// ChatHub.SendMessage or the Redis SignalR backplane.
/// </summary>
[Collection("StressTests")]
public class Scenario4_SustainedGroupMessaging : IAsyncLifetime
{
    private StressTestFactory _factory = null!;
    private DbInspector       _db      = null!;

    private const int UserCount       = 50;
    private const int DurationSeconds = 60;

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

    [Fact(DisplayName = "S4 — 50 users × 1 msg/s × 60 s = 3,000 msgs, zero delivery drops")]
    public async Task SustainedGroupMessaging_ZeroDeliveryDrops()
    {
        // ── SETUP ────────────────────────────────────────────────────────────
        var userFactory = new TestUserFactory(_factory.CreateClient());
        var users = await userFactory.CreateUsersAsync(UserCount, "s4sustained");

        // Create the shared group room via the first user's private chat, then
        // insert all remaining users directly so we get one room for all 50.
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", users[0].Token);

        var roomResp = await adminClient.PostAsync(
            $"/api/ChatRooms/private/{users[1].UserId}", null);
        roomResp.EnsureSuccessStatusCode();
        var roomJson = await roomResp.Content.ReadFromJsonAsync<JsonElement>();
        var roomId   = roomJson.GetProperty("id").GetInt32();

        // Add users 2..49 to the room via the DB directly (no group-room API endpoint)
        using var scope  = _factory.Services.CreateScope();
        var       dbCtx  = scope.ServiceProvider
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
        var received = new ConcurrentDictionary<int, int>(); // userId → msg count
        foreach (var u in users) received[u.UserId] = 0;

        var reconnections = new ConcurrentBag<int>(); // track any reconnect events

        await using var pool = SignalRClientPool.Create(_factory, users);

        // Wire up handlers before starting
        for (var i = 0; i < pool.Connections.Count; i++)
        {
            var userId = users[i].UserId;
            pool.Connections[i].On<JsonElement>("ReceiveMessage", _ =>
            {
                received.AddOrUpdate(userId, 1, (_, old) => old + 1);
            });
            pool.Connections[i].Reconnected += _ =>
            {
                reconnections.Add(userId);
                return Task.CompletedTask;
            };
        }

        await pool.StartAllAsync();
        await pool.InvokeAllAsync("JoinRoom", roomId);
        await Task.Delay(200); // let group joins propagate

        // ── ACT: sustained 1 msg/s per user for DurationSeconds ──────────────
        var sendErrors   = new ConcurrentBag<string>();
        var expectedMsgs = UserCount * DurationSeconds; // 3,000

        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(DurationSeconds + 15)); // hard outer timeout

        var senderTasks = pool.Connections.Select((conn, i) =>
            Task.Run(async () =>
            {
                for (var tick = 0; tick < DurationSeconds; tick++)
                {
                    var sendStart = DateTimeOffset.UtcNow;
                    try
                    {
                        await conn.InvokeAsync("SendMessage", roomId,
                            $"[U{users[i].UserId}|t{tick}]", cts.Token);
                    }
                    catch (Exception ex)
                    {
                        sendErrors.Add($"U{users[i].UserId} tick {tick}: {ex.Message}");
                    }

                    // Pace to ~1 msg/s, accounting for send latency
                    var elapsed = DateTimeOffset.UtcNow - sendStart;
                    var delay   = TimeSpan.FromSeconds(1) - elapsed;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, cts.Token);
                }
            }, cts.Token));

        await Task.WhenAll(senderTasks);

        // Give all in-flight messages time to arrive at all clients
        await Task.Delay(3000);

        // ── ASSERT ───────────────────────────────────────────────────────────
        sendErrors.Should().BeEmpty(
            $"no SendMessage calls should throw; errors: {string.Join("; ", sendErrors)}");

        // DB truth: all messages were persisted
        var dbCount = await _db.CountMessagesInRoomAsync(roomId);
        dbCount.Should().Be(expectedMsgs,
            $"DB must contain all {expectedMsgs} messages (no lost writes)");

        // Delivery truth: every user received every message
        var drops = received
            .Where(kv => kv.Value < expectedMsgs)
            .Select(kv => $"U{kv.Key}: {kv.Value}/{expectedMsgs}")
            .ToList();

        drops.Should().BeEmpty(
            $"every user must receive all {expectedMsgs} messages; " +
            $"users with drops: {string.Join(", ", drops)}");

        reconnections.Should().BeEmpty(
            "no SignalR connections should have been forced to reconnect " +
            "during the 60-second sustained test");
    }
}
