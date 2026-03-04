using ChatApp.Core.Entities;
using ChatApp.Core.Interfaces;
using ChatApp.Infrastructure.Services;
using Moq;
using Xunit;

namespace ChatApp.UnitTests.Services;

public class ChatServiceTests
{
    private readonly Mock<IChatRoomRepository> _mockRoomRepository;
    private readonly ChatService _chatService;

    public ChatServiceTests()
    {
        _mockRoomRepository = new Mock<IChatRoomRepository>();
        _chatService = new ChatService(_mockRoomRepository.Object);
    }

    // ── GetUserRoomsAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetUserRoomsAsync_ShouldDelegateToRepository()
    {
        // Arrange
        var expectedRooms = new List<ChatRoom>
        {
            new() { Id = 1, Name = "Room 1", Type = "Group" },
            new() { Id = 2, Name = "Room 2", Type = "Private" }
        };
        _mockRoomRepository
            .Setup(r => r.GetUserRoomsAsync(42))
            .ReturnsAsync(expectedRooms);

        // Act
        var result = await _chatService.GetUserRoomsAsync(42);

        // Assert
        Assert.Equal(expectedRooms, result);
        _mockRoomRepository.Verify(r => r.GetUserRoomsAsync(42), Times.Once);
    }

    [Fact]
    public async Task GetUserRoomsAsync_ShouldReturnEmpty_WhenUserHasNoRooms()
    {
        // Arrange
        _mockRoomRepository
            .Setup(r => r.GetUserRoomsAsync(99))
            .ReturnsAsync(Enumerable.Empty<ChatRoom>());

        // Act
        var result = await _chatService.GetUserRoomsAsync(99);

        // Assert
        Assert.Empty(result);
    }

    // ── GetOrCreatePrivateRoomAsync ─────────────────────────────────

    [Fact]
    public async Task GetOrCreatePrivateRoomAsync_ShouldReturnExistingRoom_WhenOneExists()
    {
        // Arrange
        var existingRoom = new ChatRoom { Id = 10, Name = "Private Chat", Type = "Private" };
        _mockRoomRepository
            .Setup(r => r.GetPrivateRoomAsync(1, 2))
            .ReturnsAsync(existingRoom);

        // Act
        var result = await _chatService.GetOrCreatePrivateRoomAsync(1, 2);

        // Assert
        Assert.Equal(10, result.Id);
        _mockRoomRepository.Verify(r => r.CreateAsync(It.IsAny<ChatRoom>()), Times.Never);
        _mockRoomRepository.Verify(r => r.AddUserToRoomAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreatePrivateRoomAsync_ShouldCreateNewRoom_WhenNoneExists()
    {
        // Arrange
        _mockRoomRepository
            .Setup(r => r.GetPrivateRoomAsync(1, 2))
            .ReturnsAsync((ChatRoom?)null);

        _mockRoomRepository
            .Setup(r => r.CreateAsync(It.IsAny<ChatRoom>()))
            .ReturnsAsync((ChatRoom room) =>
            {
                room.Id = 100; // simulate DB-generated ID
                return room;
            });

        // Act
        var result = await _chatService.GetOrCreatePrivateRoomAsync(1, 2);

        // Assert — room was created
        Assert.Equal(100, result.Id);
        Assert.Equal("Private", result.Type);
        Assert.Equal("Private Chat", result.Name);

        // Assert — both participants were added
        _mockRoomRepository.Verify(r => r.CreateAsync(It.Is<ChatRoom>(
            room => room.Type == "Private" && room.Name == "Private Chat")), Times.Once);
        _mockRoomRepository.Verify(r => r.AddUserToRoomAsync(100, 1), Times.Once);
        _mockRoomRepository.Verify(r => r.AddUserToRoomAsync(100, 2), Times.Once);
    }

    [Fact]
    public async Task GetOrCreatePrivateRoomAsync_ShouldNotCreateDuplicates_ForSameUserIds()
    {
        // Arrange — simulate that a room already exists for user 5 ↔ 5
        var selfRoom = new ChatRoom { Id = 50, Name = "Private Chat", Type = "Private" };
        _mockRoomRepository
            .Setup(r => r.GetPrivateRoomAsync(5, 5))
            .ReturnsAsync(selfRoom);

        // Act
        var result = await _chatService.GetOrCreatePrivateRoomAsync(5, 5);

        // Assert
        Assert.Equal(50, result.Id);
        _mockRoomRepository.Verify(r => r.CreateAsync(It.IsAny<ChatRoom>()), Times.Never);
    }
}
