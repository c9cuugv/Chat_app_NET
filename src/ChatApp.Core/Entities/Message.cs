namespace ChatApp.Core.Entities;

public class Message
{
    public int Id { get; set; }
    
    public int RoomId { get; set; }
    public ChatRoom ChatRoom { get; set; } = null!;

    public int SenderId { get; set; }
    public User Sender { get; set; } = null!;

    public required string Content { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
