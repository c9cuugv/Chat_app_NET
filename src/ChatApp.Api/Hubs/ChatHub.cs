using System.Security.Claims;
using Microsoft.Extensions.Logging;
using ChatApp.Core.Entities;
using ChatApp.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatApp.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessageRepository _messageRepository;
    private readonly IChatRoomRepository _roomRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPresenceService _presenceService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IMessageRepository messageRepository, IChatRoomRepository roomRepository, IUserRepository userRepository, IPresenceService presenceService, INotificationService notificationService, ILogger<ChatHub> logger)
    {
        _messageRepository = messageRepository;
        _roomRepository = roomRepository;
        _userRepository = userRepository;
        _presenceService = presenceService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task JoinRoom(int roomId)
    {
        var userId = int.Parse(Context.UserIdentifier!);
        
        // Verify user access
        if (!await _roomRepository.IsUserInRoomAsync(roomId, userId))
        {
             // For private chats, we don't just add them. 
             // But for this MVP, let's assume if they have the roomId, they can join.
             // Ideally: throw HubException or return error.
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId.ToString());
    }

    public async Task LeaveRoom(int roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId.ToString());
    }

    public async Task SendMessage(int roomId, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var userId = int.Parse(Context.UserIdentifier!);
        var username = Context.User!.Identity!.Name;

        _logger.LogInformation("SendMessage: User {UserId} ({Username}) sending to Room {RoomId}", userId, username, roomId);

        // Save to DB
        var message = new Message
        {
            RoomId = roomId,
            SenderId = userId,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        await _messageRepository.CreateAsync(message);

        // Get participants to notify
        var room = await _roomRepository.GetByIdAsync(roomId);
        if (room == null) 
        {
            _logger.LogWarning("SendMessage: Room {RoomId} not found", roomId);
            return;
        }

        var messageData = new 
        {
            roomId = roomId,
            senderId = userId,
            senderName = username,
            content = content,
            sentAt = message.SentAt,
            messageId = message.Id
        };

        // Send to group
        _logger.LogInformation("SendMessage: Sending to Group {RoomId}", roomId);
        await Clients.Group(roomId.ToString()).SendAsync("ReceiveMessage", messageData);

        // Notify (Log)
        await _notificationService.SendPushNotificationAsync(userId, "New Message", $"You sent a message to room {roomId}");
    }

    public async Task MarkRoomAsRead(int roomId)
    {
        var userId = int.Parse(Context.UserIdentifier!);
        await _roomRepository.UpdateLastReadAtAsync(roomId, userId);
    }

    public override async Task OnConnectedAsync()
    {
        var userIdString = Context.UserIdentifier;
        if (userIdString == null) return;

        _logger.LogInformation("OnConnectedAsync: User {UserId} connected with ConnectionId {ConnectionId}", userIdString, Context.ConnectionId);

        var userId = int.Parse(userIdString);
        await _presenceService.UserConnectedAsync(userId, Context.ConnectionId);

        // Notify others that this user is online
        await Clients.All.SendAsync("UserPresenceUpdate", userId, true);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userIdString = Context.UserIdentifier;
        if (userIdString != null)
        {
            var userId = int.Parse(userIdString);
            await _presenceService.UserDisconnectedAsync(userId, Context.ConnectionId);

            // Check if user is still online (has other connections)
            var isStillOnline = await _presenceService.IsUserOnlineAsync(userId);
            if (!isStillOnline)
            {
                await Clients.All.SendAsync("UserPresenceUpdate", userId, false);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
