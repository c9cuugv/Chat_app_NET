using System;
using System.Threading.Tasks;
using ChatApp.Api.Hubs;
using ChatApp.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChatApp.UnitTests.Hubs;

public class ChatHubTests
{
    private readonly Mock<IMessageRepository> _mockMessageRepository;
    private readonly Mock<IChatRoomRepository> _mockRoomRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IPresenceService> _mockPresenceService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ILogger<ChatHub>> _mockLogger;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly ChatHub _chatHub;

    public ChatHubTests()
    {
        _mockMessageRepository = new Mock<IMessageRepository>();
        _mockRoomRepository = new Mock<IChatRoomRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockPresenceService = new Mock<IPresenceService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<ChatHub>>();
        _mockContext = new Mock<HubCallerContext>();
        _mockGroups = new Mock<IGroupManager>();

        _chatHub = new ChatHub(
            _mockMessageRepository.Object,
            _mockRoomRepository.Object,
            _mockUserRepository.Object,
            _mockPresenceService.Object,
            _mockNotificationService.Object,
            _mockLogger.Object
        );

        _chatHub.Context = _mockContext.Object;
        _chatHub.Groups = _mockGroups.Object;
    }

    [Fact]
    public async Task JoinRoom_UserNotInRoom_ThrowsHubException()
    {
        // Arrange
        int roomId = 1;
        int userId = 123;
        _mockContext.Setup(c => c.UserIdentifier).Returns(userId.ToString());

        _mockRoomRepository.Setup(r => r.IsUserInRoomAsync(roomId, userId))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<HubException>(() => _chatHub.JoinRoom(roomId));
    }

    [Fact]
    public async Task JoinRoom_UserInRoom_AddsToGroup()
    {
        // Arrange
        int roomId = 1;
        int userId = 123;
        string connectionId = "conn123";

        _mockContext.Setup(c => c.UserIdentifier).Returns(userId.ToString());
        _mockContext.Setup(c => c.ConnectionId).Returns(connectionId);

        _mockRoomRepository.Setup(r => r.IsUserInRoomAsync(roomId, userId))
            .ReturnsAsync(true);

        // Act
        await _chatHub.JoinRoom(roomId);

        // Assert
        _mockGroups.Verify(g => g.AddToGroupAsync(connectionId, roomId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
