using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatApp.Mobile.Services;
using ChatApp.Core.Entities;
using System.Net.Http.Json;
using ChatApp.Mobile.Views;

namespace ChatApp.Mobile.ViewModels;

public partial class ChatRoomsViewModel : BaseViewModel
{
    private readonly RemoteAuthService _authService;
    private readonly HttpClient _httpClient;

    public ObservableCollection<ChatRoom> Rooms { get; } = new();

    public ChatRoomsViewModel(RemoteAuthService authService)
    {
        _authService = authService;
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(RemoteAuthService.BaseUrl);
        Title = "Chat Rooms";
    }

    [RelayCommand]
    async Task GetRoomsAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            var token = await _authService.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                await Shell.Current.GoToAsync("//LoginPage");
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var rooms = await _httpClient.GetFromJsonAsync<List<ChatRoom>>("api/chatrooms");

            if (rooms != null)
            {
                Rooms.Clear();
                foreach(var room in rooms)
                    Rooms.Add(room);
            }
        }
        catch(Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Unable to get rooms: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task GoToChatAsync(ChatRoom room)
    {
        if (room == null) return;

        await Shell.Current.GoToAsync($"{nameof(ChatPage)}?roomId={room.Id}&roomName={room.Name}");
    }
}
