using ChatApp.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ChatApp.Infrastructure.Services;

public class ConsoleNotificationService : INotificationService
{
    private readonly ILogger<ConsoleNotificationService> _logger;

    public ConsoleNotificationService(ILogger<ConsoleNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendPushNotificationAsync(int userId, string title, string message)
    {
        _logger.LogInformation("PUSH NOTIFICATION [To: {UserId}]: {Title} - {Message}", userId, title, message);
        return Task.CompletedTask;
    }
}
