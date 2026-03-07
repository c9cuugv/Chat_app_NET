using StackExchange.Redis;

namespace ChatApp.StressTests.Infrastructure;

/// <summary>
/// Reads directly from Redis to assert presence state, bypassing the
/// application layer to get ground truth about what's actually stored.
/// Key layout must match PresenceService.cs exactly.
/// </summary>
public sealed class RedisInspector : IDisposable
{
    // Must match the constant in PresenceService.cs
    private const string OnlineUsersKey = "online_users";

    private readonly ConnectionMultiplexer _mux;
    private readonly IDatabase _db;

    public RedisInspector(string connectionString = "localhost:6379")
    {
        _mux = ConnectionMultiplexer.Connect(connectionString);
        _db  = _mux.GetDatabase();
    }

    /// <summary>
    /// Returns the raw hash: userId string → connection count.
    /// A positive count means the user is online.
    /// </summary>
    public async Task<Dictionary<string, long>> GetRawPresenceAsync()
    {
        var entries = await _db.HashGetAllAsync(OnlineUsersKey);
        return entries.ToDictionary(
            e => e.Name.ToString(),
            e => (long)e.Value);
    }

    /// <summary>Number of distinct users with connection count > 0 (truly online).</summary>
    public async Task<int> GetOnlineCountAsync()
    {
        var raw = await GetRawPresenceAsync();
        return raw.Count(kv => kv.Value > 0);
    }

    /// <summary>
    /// Returns user IDs whose connection count has drifted to ≤ 0.
    /// Non-empty result means the PresenceService disconnect race fired.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetStaleEntriesAsync()
    {
        var raw = await GetRawPresenceAsync();
        return raw.Where(kv => kv.Value <= 0).Select(kv => kv.Key).ToList();
    }

    /// <summary>Deletes the presence key — call before each scenario for a clean slate.</summary>
    public Task ClearPresenceAsync() => _db.KeyDeleteAsync(OnlineUsersKey);

    public void Dispose() => _mux.Dispose();
}
