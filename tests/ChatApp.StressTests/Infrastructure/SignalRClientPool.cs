using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChatApp.StressTests.Infrastructure;

/// <summary>
/// Manages a pool of authenticated SignalR HubConnections routed through the
/// in-process test server — no real TCP port needed.
/// </summary>
public sealed class SignalRClientPool : IAsyncDisposable
{
    private readonly List<HubConnection> _connections = [];
    private readonly List<TestUser> _users = [];

    /// <summary>
    /// Builds one HubConnection per user, authenticated with their JWT.
    /// Connections are NOT started until <see cref="StartAllAsync"/> is called.
    /// </summary>
    public static SignalRClientPool Create(
        WebApplicationFactory<Program> factory,
        IReadOnlyList<TestUser> users)
    {
        var pool = new SignalRClientPool();

        foreach (var user in users)
        {
            var captured = user;
            var conn = new HubConnectionBuilder()
                .WithUrl(
                    new Uri(factory.Server.BaseAddress, "chatHub"),
                    opts =>
                    {
                        opts.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                        opts.AccessTokenProvider = () =>
                            Task.FromResult<string?>(captured.Token);
                    })
                .WithAutomaticReconnect()
                .Build();

            pool._connections.Add(conn);
            pool._users.Add(user);
        }

        return pool;
    }

    public IReadOnlyList<HubConnection> Connections => _connections;
    public IReadOnlyList<TestUser>      Users        => _users;

    /// <summary>Starts all connections simultaneously and waits for all to reach Connected.</summary>
    public Task StartAllAsync(CancellationToken ct = default)
        => Task.WhenAll(_connections.Select(c => c.StartAsync(ct)));

    /// <summary>Stops all connections simultaneously.</summary>
    public Task StopAllAsync(CancellationToken ct = default)
        => Task.WhenAll(_connections.Select(c => c.StopAsync(ct)));

    /// <summary>Invokes the same hub method on every connection simultaneously.</summary>
    public Task InvokeAllAsync(string method, object? arg1, CancellationToken ct = default)
        => Task.WhenAll(_connections.Select(c => c.InvokeAsync(method, arg1, ct)));

    /// <summary>
    /// Registers the same <paramref name="handler"/> on all connections for
    /// <paramref name="eventName"/>. Must be called before StartAllAsync.
    /// </summary>
    public void OnAll<T>(string eventName, Action<T> handler)
    {
        foreach (var conn in _connections)
            conn.On(eventName, handler);
    }

    /// <summary>
    /// Registers the same two-argument <paramref name="handler"/> on all connections for
    /// <paramref name="eventName"/>. Must be called before StartAllAsync.
    /// </summary>
    public void OnAll<T1, T2>(string eventName, Action<T1, T2> handler)
    {
        foreach (var conn in _connections)
            conn.On(eventName, handler);
    }

    public int ConnectedCount =>
        _connections.Count(c => c.State == HubConnectionState.Connected);

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync();
        foreach (var c in _connections)
            await c.DisposeAsync();
    }
}
