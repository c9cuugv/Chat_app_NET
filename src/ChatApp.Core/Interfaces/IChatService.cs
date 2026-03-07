using ChatApp.Core.Entities;

namespace ChatApp.Core.Interfaces;

/// <summary>
/// Application service that orchestrates chat room business logic,
/// separating domain operations from HTTP/transport concerns.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Retrieves all chat rooms that a given user is a participant of.
    /// </summary>
    Task<IEnumerable<ChatRoom>> GetUserRoomsAsync(int userId);

    /// <summary>
    /// Returns an existing private room between two users,
    /// or creates one (with participants) if none exists.
    /// </summary>
    Task<ChatRoom> GetOrCreatePrivateRoomAsync(int currentUserId, int otherUserId);
}
