using ChatApp.Core.Entities;
using ChatApp.Core.Interfaces;

namespace ChatApp.Infrastructure.Services;

/// <summary>
/// Orchestrates chat room operations by coordinating repository calls.
/// Keeps controllers thin and business logic testable in isolation.
/// </summary>
public class ChatService : IChatService
{
    private const string PrivateRoomType = "Private";
    private const string DefaultPrivateRoomName = "Private Chat";

    private readonly IChatRoomRepository _roomRepository;

    public ChatService(IChatRoomRepository roomRepository)
    {
        _roomRepository = roomRepository;
    }

    public async Task<IEnumerable<ChatRoom>> GetUserRoomsAsync(int userId)
    {
        return await _roomRepository.GetUserRoomsAsync(userId);
    }

    public async Task<ChatRoom> GetOrCreatePrivateRoomAsync(int currentUserId, int otherUserId)
    {
        var existingRoom = await _roomRepository.GetPrivateRoomAsync(currentUserId, otherUserId);
        if (existingRoom is not null)
        {
            return existingRoom;
        }

        var newRoom = new ChatRoom
        {
            Name = DefaultPrivateRoomName,
            Type = PrivateRoomType,
            CreatedAt = DateTime.UtcNow
        };

        newRoom = await _roomRepository.CreateAsync(newRoom);

        await _roomRepository.AddUserToRoomAsync(newRoom.Id, currentUserId);
        await _roomRepository.AddUserToRoomAsync(newRoom.Id, otherUserId);

        return newRoom;
    }
}
