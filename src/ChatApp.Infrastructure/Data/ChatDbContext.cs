using ChatApp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Infrastructure.Data;

public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<ChatRoom> ChatRooms { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<RoomParticipant> RoomParticipants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Username).HasColumnName("username").IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).HasColumnName("email").IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasColumnName("passwordhash").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("createdat");
            
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.ToTable("chatrooms");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.CreatedAt).HasColumnName("createdat");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("messages");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RoomId).HasColumnName("roomid");
            entity.Property(e => e.SenderId).HasColumnName("senderid");
            entity.Property(e => e.Content).HasColumnName("content").HasMaxLength(2000);
            entity.Property(e => e.SentAt).HasColumnName("sentat");
            entity.Property(e => e.IsRead).HasColumnName("isread");
            
            entity.HasOne(m => m.ChatRoom)
                .WithMany(r => r.Messages)
                .HasForeignKey(m => m.RoomId);

            entity.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId);
        });

        modelBuilder.Entity<RoomParticipant>(entity =>
        {
            entity.ToTable("roomparticipants");
            entity.HasKey(rp => new { rp.RoomId, rp.UserId });
            entity.Property(rp => rp.RoomId).HasColumnName("roomid");
            entity.Property(rp => rp.UserId).HasColumnName("userid");
            entity.Property(rp => rp.JoinedAt).HasColumnName("joinedat");
            entity.Property(rp => rp.LastReadAt).HasColumnName("lastreadat");

            entity.HasOne(rp => rp.ChatRoom)
                .WithMany(r => r.Participants)
                .HasForeignKey(rp => rp.RoomId);

            entity.HasOne(rp => rp.User)
                .WithMany()
                .HasForeignKey(rp => rp.UserId);
        });
    }
}
