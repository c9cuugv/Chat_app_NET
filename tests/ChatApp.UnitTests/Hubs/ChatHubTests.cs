using ChatApp.Api.Hubs;
using ChatApp.Core.Entities;
using ChatApp.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using Xunit;
using System.Security.Claims;

namespace ChatApp.UnitTests.Hubs;

public class ChatHubTests
{
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IChatRoomRepository> _roomRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPresenceService> _presenceServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ILogger<ChatHub>> _loggerMock;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Mock<HubCallerContext> _contextMock;
    private readonly ChatHub _chatHub;

    public ChatHubTests()
    {
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _roomRepositoryMock = new Mock<IChatRoomRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _presenceServiceMock = new Mock<IPresenceService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _loggerMock = new Mock<ILogger<ChatHub>>();

        _clientsMock = new Mock<IHubCallerClients>();
        _clientProxyMock = new Mock<IClientProxy>();
        _contextMock = new Mock<HubCallerContext>();

        // Setup Clients.User(...) to return the proxy
        _clientsMock.Setup(c => c.User(It.IsAny<string>())).Returns(_clientProxyMock.Object);
        _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
        _clientsMock.Setup(c => c.All).Returns(_clientProxyMock.Object);

        _chatHub = new ChatHub(
            _messageRepositoryMock.Object,
            _roomRepositoryMock.Object,
            _userRepositoryMock.Object,
            _presenceServiceMock.Object,
            _notificationServiceMock.Object,
            _loggerMock.Object
        )
        {
            Clients = _clientsMock.Object,
            Context = _contextMock.Object
        };
    }

    [Fact]
    public async Task SendMessage_ShouldSendToGroup_AndNotLoopParticipants()
    {
        // Arrange
        int roomId = 1;
        int senderId = 999;
        string content = "Hello World";
        int participantCount = 1000;

        // Setup Context User
        _contextMock.Setup(c => c.UserIdentifier).Returns(senderId.ToString());
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "TestUser")
        });
        var principal = new ClaimsPrincipal(identity);
        _contextMock.Setup(c => c.User).Returns(principal);

        // Setup Room with Participants
        var participants = new List<RoomParticipant>();
        for (int i = 0; i < participantCount; i++)
        {
            participants.Add(new RoomParticipant { UserId = i, RoomId = roomId });
        }

        var room = new ChatRoom
        {
            Id = roomId,
            Name = "Benchmark Room",
            Type = "Group",
            Participants = participants
        };

        _roomRepositoryMock.Setup(r => r.GetByIdAsync(roomId))
            .ReturnsAsync(room);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _chatHub.SendMessage(roomId, content);
        stopwatch.Stop();

        // Assert
        // Optimized implementation: Should NOT call Clients.User for participants
        _clientsMock.Verify(c => c.User(It.IsAny<string>()), Times.Never);

        // Should call Clients.Group(roomId) exactly once
        _clientsMock.Verify(c => c.Group(roomId.ToString()), Times.Once);

        Console.WriteLine($"SendMessage with {participantCount} participants took {stopwatch.ElapsedMilliseconds} ms");
    }
}
