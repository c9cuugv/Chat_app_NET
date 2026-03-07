using System.Net.Http.Json;
using System.Text.Json;

namespace ChatApp.Mobile.Services;

/// <summary>
/// Handles authentication against the remote ChatApp API and persists the
/// resulting JWT token and user-id in secure device storage.
/// </summary>
public class RemoteAuthService
{
    private readonly HttpClient _httpClient;

    // Android emulator routes localhost through the host machine at 10.0.2.2
    public static string BaseUrl = DeviceInfo.Platform == DevicePlatform.Android
        ? "http://10.0.2.2:5200"
        : "http://localhost:5200";

    public RemoteAuthService()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
    }

    public async Task<(bool Success, string Message)> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(content);
                var token = result.GetProperty("token").GetString();
                var userId = result.GetProperty("userId").GetInt32();

                if (token != null)
                {
                    await SecureStorage.SetAsync("auth_token", token);
                    await SecureStorage.SetAsync("user_id", userId.ToString());
                    return (true, "Login successful");
                }
            }
            return (false, "Invalid credentials");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public void Logout()
    {
        SecureStorage.Remove("auth_token");
        SecureStorage.Remove("user_id");
    }

    public async Task<string?> GetTokenAsync() =>
        await SecureStorage.GetAsync("auth_token");

    public async Task<int> GetUserIdAsync()
    {
        var id = await SecureStorage.GetAsync("user_id");
        return int.Parse(id ?? "0");
    }
}
