using ChatApp.Core.Entities;

namespace ChatApp.Core.Interfaces;

public interface IConnectionRepository
{
    Task<ConnectionRequest> SendRequestAsync(int senderId, int receiverId);
    Task<ConnectionRequest?> GetRequestAsync(int requestId);
    Task<ConnectionRequest?> GetRequestBetweenAsync(int user1Id, int user2Id);
    Task<IEnumerable<ConnectionRequest>> GetPendingReceivedAsync(int userId);
    Task<IEnumerable<ConnectionRequest>> GetPendingSentAsync(int userId);
    Task<IEnumerable<User>> GetConnectionsAsync(int userId);
    Task<IEnumerable<User>> GetDiscoverableUsersAsync(int userId);
    Task AcceptAsync(int requestId);
    Task RejectAsync(int requestId);
}
