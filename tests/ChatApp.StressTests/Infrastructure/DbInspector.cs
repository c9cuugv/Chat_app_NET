using ChatApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.StressTests.Infrastructure;

/// <summary>
/// Reads directly from Postgres to assert ground-truth data state —
/// message counts, room counts, duplicate detection — without going
/// through the application layer.
/// </summary>
public sealed class DbInspector(string connectionString = StressTestFactory.ConnectionString)
{
    private readonly string _connectionString = connectionString;

    private ChatDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<ChatDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new ChatDbContext(opts);
    }

    /// <summary>Exact count of messages persisted in a given room.</summary>
    public async Task<int> CountMessagesInRoomAsync(int roomId)
    {
        await using var ctx = CreateContext();
        return await ctx.Messages.CountAsync(m => m.RoomId == roomId);
    }

    /// <summary>
    /// Number of "Private" rooms that contain BOTH users as participants.
    /// Should always be exactly 1. > 1 means the duplicate-room race fired.
    /// </summary>
    public async Task<int> CountPrivateRoomsBetweenAsync(int user1Id, int user2Id)
    {
        await using var ctx = CreateContext();
        return await ctx.ChatRooms
            .Where(r => r.Type == "Private")
            .Where(r =>
                r.Participants.Any(p => p.UserId == user1Id) &&
                r.Participants.Any(p => p.UserId == user2Id))
            .CountAsync();
    }

    /// <summary>Total rooms in the DB (any type).</summary>
    public async Task<int> CountAllRoomsAsync()
    {
        await using var ctx = CreateContext();
        return await ctx.ChatRooms.CountAsync();
    }

    /// <summary>Total messages across all rooms.</summary>
    public async Task<int> CountAllMessagesAsync()
    {
        await using var ctx = CreateContext();
        return await ctx.Messages.CountAsync();
    }

    /// <summary>
    /// Returns a dictionary of roomId → message count for all rooms.
    /// Useful for spotting unexpected message routing during burst tests.
    /// </summary>
    public async Task<Dictionary<int, int>> MessageCountPerRoomAsync()
    {
        await using var ctx = CreateContext();
        return await ctx.Messages
            .GroupBy(m => m.RoomId)
            .Select(g => new { RoomId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoomId, x => x.Count);
    }
}
