using ChatApp.Core.Entities;

namespace ChatApp.Core.Interfaces;

public interface IChatRoomRepository
{
    Task<ChatRoom?> GetByIdAsync(int id);
    Task<IEnumerable<ChatRoom>> GetUserRoomsAsync(int userId);
    Task<ChatRoom> CreateAsync(ChatRoom room);
    Task AddUserToRoomAsync(int roomId, int userId);
    Task UpdateLastReadAtAsync(int roomId, int userId);
    Task<ChatRoom?> GetPrivateRoomAsync(int user1Id, int user2Id);
    Task<ChatRoom> GetOrCreatePrivateRoomAsync(int user1Id, int user2Id);
    Task<bool> IsUserInRoomAsync(int roomId, int userId);
}
