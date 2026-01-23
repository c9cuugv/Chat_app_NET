using ChatApp.Core.Entities;

namespace ChatApp.Core.Interfaces;

public interface INotificationService
{
    Task SendPushNotificationAsync(int userId, string title, string message);
}
