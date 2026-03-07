using ChatApp.Core.Entities;
using ChatApp.Core.Interfaces;
using ChatApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Infrastructure.Repositories;

public class ChatRoomRepository : IChatRoomRepository
{
    private readonly ChatDbContext _context;

    public ChatRoomRepository(ChatDbContext context)
    {
        _context = context;
    }

    public async Task AddUserToRoomAsync(int roomId, int userId)
    {
        var participant = new RoomParticipant
        {
            RoomId = roomId,
            UserId = userId
        };
        _context.RoomParticipants.Add(participant);
        await _context.SaveChangesAsync();
    }

    public async Task<ChatRoom> CreateAsync(ChatRoom room)
    {
        _context.ChatRooms.Add(room);
        await _context.SaveChangesAsync();
        return room;
    }

    public async Task<ChatRoom?> GetByIdAsync(int id)
    {
        return await _context.ChatRooms
            .Include(r => r.Participants)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<ChatRoom>> GetUserRoomsAsync(int userId)
    {
        return await _context.ChatRooms
            .Include(r => r.Participants)
            .Where(r => r.Participants.Any(p => p.UserId == userId))
            .ToListAsync();
    }

    public async Task<bool> IsUserInRoomAsync(int roomId, int userId)
    {
        return await _context.RoomParticipants
            .AnyAsync(p => p.RoomId == roomId && p.UserId == userId);
    }

    public async Task<ChatRoom?> GetPrivateRoomAsync(int user1Id, int user2Id)
    {
        return await _context.ChatRooms
            .Where(r => r.Type == "Private")
            .FirstOrDefaultAsync(r =>
                r.Participants.Any(p => p.UserId == user1Id) &&
                r.Participants.Any(p => p.UserId == user2Id));
    }

    public async Task<ChatRoom> GetOrCreatePrivateRoomAsync(int user1Id, int user2Id)
    {
        // Use a serializable transaction to prevent the race condition where
        // two concurrent requests both see no room and both create one.
        await using var transaction = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        try
        {
            var existing = await _context.ChatRooms
                .Where(r => r.Type == "Private")
                .FirstOrDefaultAsync(r =>
                    r.Participants.Any(p => p.UserId == user1Id) &&
                    r.Participants.Any(p => p.UserId == user2Id));

            if (existing != null)
            {
                await transaction.CommitAsync();
                return existing;
            }

            var room = new ChatRoom
            {
                Name = "Private Chat",
                Type = "Private",
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatRooms.Add(room);
            await _context.SaveChangesAsync();

            _context.RoomParticipants.AddRange(
                new RoomParticipant { RoomId = room.Id, UserId = user1Id },
                new RoomParticipant { RoomId = room.Id, UserId = user2Id }
            );
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return room;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateLastReadAtAsync(int roomId, int userId)
    {
        var participant = await _context.RoomParticipants
            .FirstOrDefaultAsync(p => p.RoomId == roomId && p.UserId == userId);

        if (participant != null)
        {
            participant.LastReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
