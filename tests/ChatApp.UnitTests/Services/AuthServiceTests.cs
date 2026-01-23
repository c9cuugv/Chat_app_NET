using ChatApp.Core.Entities;
using ChatApp.Core.Interfaces;
using ChatApp.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ChatApp.UnitTests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // Mock default config
        _mockConfiguration.Setup(c => c.GetSection(It.IsAny<string>())).Returns(new Mock<IConfigurationSection>().Object);

        _authService = new AuthService(_mockUserRepository.Object, _mockConfiguration.Object);
    }

    [Fact]
    public async Task RegisterAsync_ShouldReturnError_WhenEmailExists()
    {
        // Arrange
        _mockUserRepository.Setup(x => x.ExistsAsync("test@example.com")).ReturnsAsync(true);

        // Act
        var result = await _authService.RegisterAsync("test", "test@example.com", "password");

        // Assert
        Assert.Null(result.User);
        Assert.Equal("Email already in use", result.ErrorMessage);
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateUser_WhenEmailIsUnique()
    {
        // Arrange
        _mockUserRepository.Setup(x => x.ExistsAsync("test@example.com")).ReturnsAsync(false);
        _mockUserRepository.Setup(x => x.CreateAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);

        // Act
        var result = await _authService.RegisterAsync("test", "test@example.com", "password");

        // Assert
        Assert.NotNull(result.User);
        Assert.Equal("test", result.User!.Username);
        Assert.Empty(result.ErrorMessage);
    }
}
