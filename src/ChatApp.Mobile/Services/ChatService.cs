using Microsoft.AspNetCore.SignalR.Client;

namespace ChatApp.Mobile.Services;

public class ChatService
{
    private HubConnection? _hubConnection;
    private readonly AuthService _authService;

    public event Action<int, int, string, string>? OnMessageReceived;

    public ChatService(AuthService authService)
    {
        _authService = authService;
    }

    public async Task ConnectAsync()
    {
        var token = await _authService.GetTokenAsync();
        if (string.IsNullOrEmpty(token)) return;

        var baseUrl = AuthService.BaseUrl;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/chatHub", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<MessageResponse>("ReceiveMessage", (msg) =>
        {
            OnMessageReceived?.Invoke(msg.roomId, msg.senderId, msg.senderName, msg.content);
        });

        await _hubConnection.StartAsync();
    }

    public async Task SendMessageAsync(int roomId, string content)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("SendMessage", roomId, content);
        }
    }

    public async Task JoinRoomAsync(int roomId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("JoinRoom", roomId);
        }
    }
}

public class MessageResponse
{
    public int roomId { get; set; }
    public int senderId { get; set; }
    public string senderName { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
    public DateTime sentAt { get; set; }
}
