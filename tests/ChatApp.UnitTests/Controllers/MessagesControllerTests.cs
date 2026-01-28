using System.Security.Claims;
using ChatApp.Api.Controllers;
using ChatApp.Core.Entities;
using ChatApp.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ChatApp.UnitTests.Controllers;

public class MessagesControllerTests
{
    private readonly Mock<IMessageRepository> _mockMessageRepository;
    private readonly Mock<IChatRoomRepository> _mockRoomRepository;
    private readonly MessagesController _controller;
    private readonly ClaimsPrincipal _user;

    public MessagesControllerTests()
    {
        _mockMessageRepository = new Mock<IMessageRepository>();
        _mockRoomRepository = new Mock<IChatRoomRepository>();
        _controller = new MessagesController(_mockMessageRepository.Object, _mockRoomRepository.Object);

        var userId = "1";
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "TestUser")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        _user = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = _user }
        };
    }

    [Fact]
    public async Task GetRoomMessages_ShouldReturnMessages_WhenUserIsInRoom()
    {
        // Arrange
        int roomId = 1;
        int userId = 1;
        var messages = new List<Message> { new Message { Id = 1, Content = "Hello", RoomId = roomId, SenderId = userId, SentAt = DateTime.UtcNow } };

        _mockRoomRepository.Setup(x => x.IsUserInRoomAsync(roomId, userId)).ReturnsAsync(true);
        _mockMessageRepository.Setup(x => x.GetRecentMessagesAsync(roomId, 50)).ReturnsAsync(messages);

        // Act
        var result = await _controller.GetRoomMessages(roomId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedMessages = Assert.IsAssignableFrom<IEnumerable<Message>>(okResult.Value);
        Assert.Single(returnedMessages);
    }

    [Fact]
    public async Task GetRoomMessages_ShouldReturnForbid_WhenUserIsNotInRoom()
    {
        // Arrange
        int roomId = 1;
        int userId = 1;

        _mockRoomRepository.Setup(x => x.IsUserInRoomAsync(roomId, userId)).ReturnsAsync(false);

        // Act
        var result = await _controller.GetRoomMessages(roomId);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }
}
