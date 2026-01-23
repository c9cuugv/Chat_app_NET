namespace ChatApp.Core.Entities;

public class ChatRoom
{
    public int Id { get; set; }
    public required string Name { get; set; }
    // 'Private' or 'Group'
    public required string Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RoomParticipant> Participants { get; set; } = new List<RoomParticipant>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
