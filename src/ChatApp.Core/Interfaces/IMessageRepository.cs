using ChatApp.Core.Entities;

namespace ChatApp.Core.Interfaces;

public interface IMessageRepository
{
    Task<Message> CreateAsync(Message message);
    Task<IEnumerable<Message>> GetRecentMessagesAsync(int roomId, int count = 50);
    Task MarkAsReadAsync(int messageId);
}
