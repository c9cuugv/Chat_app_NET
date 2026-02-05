using System.Threading.Tasks;
using ChatApp.Mobile.Services;
using Moq;
using Xunit;

namespace ChatApp.UnitTests.Services
{
    public class ChatServiceTests
    {
        [Fact]
        public async Task ConnectAsync_CalledMultipleTimes_CreatesNewConnectionInstance()
        {
            // Arrange
            var mockAuthService = new Mock<IAuthService>();
            mockAuthService.Setup(s => s.GetTokenAsync()).ReturnsAsync("test_token");

            var chatService = new ChatService(mockAuthService.Object);

            // Act
            // First connect
            try
            {
                await chatService.ConnectAsync();
            }
            catch
            {
                // Ignore connection error (no server running)
            }
            var connection1 = chatService.HubConnection;

            // Second connect
            try
            {
                await chatService.ConnectAsync();
            }
            catch
            {
                // Ignore connection error
            }
            var connection2 = chatService.HubConnection;

            // Assert
            Assert.NotNull(connection1);
            Assert.NotNull(connection2);
            // Current behavior: creates new instance every time (leak)
            Assert.NotSame(connection1, connection2);
        }
    }
}
