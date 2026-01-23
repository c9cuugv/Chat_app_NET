namespace ChatApp.Core.Entities;

public class RoomParticipant
{
    public int RoomId { get; set; }
    public ChatRoom ChatRoom { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastReadAt { get; set; } = DateTime.UtcNow;
}
