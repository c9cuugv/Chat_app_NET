using ChatApp.StressTests.Infrastructure;
using FluentAssertions;
using System.Net.Http.Headers;
using Xunit;

namespace ChatApp.StressTests.Scenarios;

/// <summary>
/// Scenario 2 — Private Room Creation Race Condition
///
/// For 20 user pairs (A, B), both A and B simultaneously POST
/// /api/ChatRooms/private/{otherId}. The controller does:
///
///   var room = await _roomRepository.GetPrivateRoomAsync(a, b); // null
///   room = await _roomRepository.CreateAsync(new ChatRoom(...));
///   await _roomRepository.AddUserToRoomAsync(room.Id, a);
///   await _roomRepository.AddUserToRoomAsync(room.Id, b);
///
/// With no transaction or lock, both concurrent requests can pass the
/// GetPrivateRoomAsync check simultaneously, creating TWO rooms for the
/// same pair — a classic check-then-act race.
///
/// Pass criteria:
///   - 0 HTTP 500 responses (no unhandled EF constraint violations)
///   - DbInspector.CountPrivateRoomsBetweenAsync(a, b) == 1 for all pairs
///
/// If the test fails on the count assertion, the fix is to add either:
///   (a) a unique partial index on RoomParticipants, or
///   (b) SELECT FOR UPDATE / serializable transaction in the controller.
/// </summary>
[Collection("StressTests")]
public class Scenario2_PrivateRoomRace : IAsyncLifetime
{
    private StressTestFactory _factory = null!;
    private DbInspector       _db      = null!;
    private const int Pairs = 20;

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

    [Fact(DisplayName = "S2 — 20 pairs race POST private/{id}: exactly 1 room created per pair")]
    public async Task ConcurrentPrivateRoomCreation_NoDuplicateRooms()
    {
        // ── SETUP ────────────────────────────────────────────────────────────
        var userFactory = new TestUserFactory(_factory.CreateClient());
        var users = await userFactory.CreateUsersAsync(Pairs * 2, "s2race");

        var pairs = Enumerable.Range(0, Pairs)
            .Select(i => (A: users[i * 2], B: users[i * 2 + 1]))
            .ToList();

        // ── ACT ──────────────────────────────────────────────────────────────
        // For each pair: A and B fire simultaneously for the same pair.
        // This is the exact race: two concurrent requests, same (A,B) combination,
        // no lock in ChatRoomsController.GetOrCreatePrivateRoom.
        var pairResults = await Task.WhenAll(pairs.Select(async pair =>
        {
            using var clientA = _factory.CreateClient();
            using var clientB = _factory.CreateClient();

            clientA.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", pair.A.Token);
            clientB.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", pair.B.Token);

            // Both fire simultaneously — this is the race window
            var taskA = clientA.PostAsync(
                $"/api/ChatRooms/private/{pair.B.UserId}", null);
            var taskB = clientB.PostAsync(
                $"/api/ChatRooms/private/{pair.A.UserId}", null);

            var responses = await Task.WhenAll(taskA, taskB);
            return new
            {
                Pair    = pair,
                StatusA = responses[0].StatusCode,
                StatusB = responses[1].StatusCode,
                BodyA   = await responses[0].Content.ReadAsStringAsync(),
                BodyB   = await responses[1].Content.ReadAsStringAsync()
            };
        }));

        // ── ASSERT ───────────────────────────────────────────────────────────

        // 1. Neither request should surface a 500 to the client.
        //    An unhandled EF unique-key violation from RoomParticipant's composite
        //    PK would produce a 500 — that would expose the race visibly.
        var serverErrors = pairResults
            .Where(r => r.StatusA == System.Net.HttpStatusCode.InternalServerError
                     || r.StatusB == System.Net.HttpStatusCode.InternalServerError)
            .ToList();

        serverErrors.Should().BeEmpty(
            "neither request in a concurrent pair should return HTTP 500; " +
            $"pairs with errors: {string.Join(", ", serverErrors.Select(e => $"A={e.Pair.A.UserId},B={e.Pair.B.UserId}"))}");

        // 2. The DB must contain exactly one private room per pair.
        //    > 1 means the race fired and two rooms were committed.
        var roomCounts = await Task.WhenAll(pairs.Select(async p =>
        {
            var count = await _db.CountPrivateRoomsBetweenAsync(p.A.UserId, p.B.UserId);
            return new { p.A.UserId, OtherUserId = p.B.UserId, Count = count };
        }));

        var duplicates = roomCounts.Where(r => r.Count != 1).ToList();

        duplicates.Should().BeEmpty(
            "every user pair must have exactly 1 private room — " +
            $"pairs with wrong count: {string.Join(", ", duplicates.Select(d => $"({d.UserId},{d.OtherUserId})={d.Count}"))}");
    }

    [Fact(DisplayName = "S2 — Same pair POST private/{id} 10 times sequentially: still 1 room")]
    public async Task RepeatedSequentialCreation_IdempotentRoom()
    {
        // Idempotency check: calling the endpoint 10 times for the same pair
        // must not create extra rooms (the GetOrCreate path must be stable).
        var userFactory = new TestUserFactory(_factory.CreateClient());
        var users = await userFactory.CreateUsersAsync(2, "s2idem");
        var (userA, userB) = (users[0], users[1]);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userA.Token);

        for (var i = 0; i < 10; i++)
        {
            var resp = await client.PostAsync(
                $"/api/ChatRooms/private/{userB.UserId}", null);
            resp.IsSuccessStatusCode.Should().BeTrue(
                $"iteration {i}: POST private room must succeed");
        }

        var roomCount = await _db.CountPrivateRoomsBetweenAsync(userA.UserId, userB.UserId);
        roomCount.Should().Be(1, "10 sequential calls must produce exactly 1 room");
    }
}
