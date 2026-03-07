using System.Net.Http.Json;
using System.Text.Json;

namespace ChatApp.StressTests.Infrastructure;

/// <summary>Registered user with a valid JWT token, ready to make authenticated requests.</summary>
public record TestUser(int UserId, string Username, string Email, string Password, string Token);

/// <summary>
/// Registers and logs in N users in parallel via the real API.
/// Each call generates unique credentials so test classes don't collide.
/// </summary>
public class TestUserFactory(HttpClient client)
{
    private readonly HttpClient _client = client;

    /// <summary>
    /// Creates <paramref name="count"/> users concurrently.
    /// The <paramref name="prefix"/> + a short GUID suffix makes usernames unique
    /// across scenarios even when the DB is shared within one run.
    /// </summary>
    public async Task<List<TestUser>> CreateUsersAsync(int count, string prefix = "stress")
    {
        var guid = Guid.NewGuid().ToString("N")[..8];

        var tasks = Enumerable.Range(0, count).Select(async i =>
        {
            var username = $"{prefix}_{guid}_{i}";
            var email    = $"{username}@stress.test";
            var password = "Password123!";

            // Register
            var regResp = await _client.PostAsJsonAsync("/api/Auth/register", new
            {
                username,
                email,
                password
            });
            regResp.EnsureSuccessStatusCode();
            var regJson = await regResp.Content.ReadFromJsonAsync<JsonElement>();
            var userId  = regJson.GetProperty("userId").GetInt32();

            // Login — get JWT
            var loginResp = await _client.PostAsJsonAsync("/api/Auth/login", new
            {
                email,
                password
            });
            loginResp.EnsureSuccessStatusCode();
            var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
            var token     = loginJson.GetProperty("token").GetString()!;

            return new TestUser(userId, username, email, password, token);
        });

        var results = await Task.WhenAll(tasks);
        return [.. results];
    }
}
