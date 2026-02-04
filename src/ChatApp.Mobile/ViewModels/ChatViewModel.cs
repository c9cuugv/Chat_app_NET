using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatApp.Mobile.Services;

namespace ChatApp.Mobile.ViewModels;

[QueryProperty(nameof(RoomId), "roomId")]
[QueryProperty(nameof(RoomName), "roomName")]
public partial class ChatViewModel : BaseViewModel
{
    private readonly ChatService _chatService;
    private readonly AuthService _authService;
    private int _currentUserId;

    [ObservableProperty]
    int roomId;

    [ObservableProperty]
    string roomName;

    [ObservableProperty]
    string messageText;

    public ObservableCollection<ChatMessageDisplay> Messages { get; } = new();

    public ChatViewModel(ChatService chatService, AuthService authService)
    {
        _chatService = chatService;
        _authService = authService;
        _chatService.OnMessageReceived += ChatService_OnMessageReceived;
    }

    private void ChatService_OnMessageReceived(int roomId, int senderId, string senderName, string content)
    {
        if (roomId != RoomId) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            bool isMine = senderId == _currentUserId;
            Messages.Add(new ChatMessageDisplay(senderName, content, DateTime.Now, isMine));
        });
    }

    [RelayCommand]
    async Task InitializeAsync()
    {
        Title = RoomName;
        _currentUserId = await _authService.GetUserIdAsync();

        await _chatService.ConnectAsync();
        await _chatService.JoinRoomAsync(RoomId);
    }

    [RelayCommand]
    async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageText)) return;

        await _chatService.SendMessageAsync(RoomId, MessageText);
        MessageText = string.Empty;
    }

    public void Cleanup()
    {
        _chatService.OnMessageReceived -= ChatService_OnMessageReceived;
    }
}

public record ChatMessageDisplay(string Sender, string Content, DateTime Time, bool IsMine);
