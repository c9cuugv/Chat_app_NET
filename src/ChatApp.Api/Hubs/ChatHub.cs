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
    private readonly IPresenceService _presenceService;
    private readonly ILogger<ChatHub> _logger;

    private const int MaxMessageLength = 2000;

    public ChatHub(IMessageRepository messageRepository, IChatRoomRepository roomRepository, IPresenceService presenceService, ILogger<ChatHub> logger)
    {
        _messageRepository = messageRepository;
        _roomRepository = roomRepository;
        _presenceService = presenceService;
        _logger = logger;
    }

    public async Task JoinRoom(int roomId)
    {
        var userId = int.Parse(Context.UserIdentifier!);

        if (!await _roomRepository.IsUserInRoomAsync(roomId, userId))
        {
            throw new HubException("You are not a member of this room.");
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

        if (content.Length > MaxMessageLength)
        {
            throw new HubException($"Message exceeds maximum length of {MaxMessageLength} characters.");
        }

        var userId = int.Parse(Context.UserIdentifier!);

        if (!await _roomRepository.IsUserInRoomAsync(roomId, userId))
        {
            throw new HubException("You are not a member of this room.");
        }

        var username = Context.User!.Identity!.Name;

        _logger.LogInformation("SendMessage: User {UserId} sending to Room {RoomId}", userId, roomId);

        var message = new Message
        {
            RoomId = roomId,
            SenderId = userId,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        await _messageRepository.CreateAsync(message);

        var messageData = new
        {
            roomId,
            senderId = userId,
            senderName = username,
            content,
            sentAt = message.SentAt,
            messageId = message.Id
        };

        await Clients.Group(roomId.ToString()).SendAsync("ReceiveMessage", messageData);
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

        await Clients.Others.SendAsync("UserPresenceUpdate", userId, true);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userIdString = Context.UserIdentifier;
        if (userIdString != null)
        {
            var userId = int.Parse(userIdString);
            await _presenceService.UserDisconnectedAsync(userId, Context.ConnectionId);

            var isStillOnline = await _presenceService.IsUserOnlineAsync(userId);
            if (!isStillOnline)
            {
                await Clients.Others.SendAsync("UserPresenceUpdate", userId, false);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
