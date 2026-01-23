namespace ChatApp.Core.Interfaces;

public interface IPresenceService
{
    Task UserConnectedAsync(int userId, string connectionId);
    Task UserDisconnectedAsync(int userId, string connectionId);
    Task<string[]> GetOnlineUsersAsync();
    Task<bool> IsUserOnlineAsync(int userId);
}
