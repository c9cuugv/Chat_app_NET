using ChatApp.Core.Interfaces;
using StackExchange.Redis;

namespace ChatApp.Infrastructure.Services;

public class PresenceService : IPresenceService
{
    private readonly IConnectionMultiplexer _redis;
    private const string ONLINE_USERS_KEY = "online_users";

    public PresenceService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task UserConnectedAsync(int userId, string connectionId)
    {
        var db = _redis.GetDatabase();
        // Increment connection count for user
        await db.HashIncrementAsync(ONLINE_USERS_KEY, userId.ToString(), 1);
    }

    public async Task UserDisconnectedAsync(int userId, string connectionId)
    {
        var db = _redis.GetDatabase();
        // Decrement connection count
        var newValue = await db.HashIncrementAsync(ONLINE_USERS_KEY, userId.ToString(), -1);
        
        // If count is 0 or less, remove the user from online users
        if (newValue <= 0)
        {
            await db.HashDeleteAsync(ONLINE_USERS_KEY, userId.ToString());
        }
    }

    public async Task<string[]> GetOnlineUsersAsync()
    {
        var db = _redis.GetDatabase();
        var users = await db.HashKeysAsync(ONLINE_USERS_KEY);
        return users.Select(u => u.ToString()).ToArray();
    }

    public async Task<bool> IsUserOnlineAsync(int userId)
    {
        var db = _redis.GetDatabase();
        return await db.HashExistsAsync(ONLINE_USERS_KEY, userId.ToString());
    }
}
