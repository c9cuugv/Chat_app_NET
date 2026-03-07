using ChatApp.Core.Entities;
using ChatApp.Core.Interfaces;
using ChatApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Infrastructure.Repositories;

public class ConnectionRepository : IConnectionRepository
{
    private readonly ChatDbContext _context;

    public ConnectionRepository(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<ConnectionRequest> SendRequestAsync(int senderId, int receiverId)
    {
        var request = new ConnectionRequest
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        _context.ConnectionRequests.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<ConnectionRequest?> GetRequestAsync(int requestId)
    {
        return await _context.ConnectionRequests
            .Include(r => r.Sender)
            .Include(r => r.Receiver)
            .FirstOrDefaultAsync(r => r.Id == requestId);
    }

    public async Task<ConnectionRequest?> GetRequestBetweenAsync(int user1Id, int user2Id)
    {
        return await _context.ConnectionRequests
            .FirstOrDefaultAsync(r =>
                (r.SenderId == user1Id && r.ReceiverId == user2Id) ||
                (r.SenderId == user2Id && r.ReceiverId == user1Id));
    }

    public async Task<IEnumerable<ConnectionRequest>> GetPendingReceivedAsync(int userId)
    {
        return await _context.ConnectionRequests
            .Include(r => r.Sender)
            .Where(r => r.ReceiverId == userId && r.Status == "Pending")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ConnectionRequest>> GetPendingSentAsync(int userId)
    {
        return await _context.ConnectionRequests
            .Include(r => r.Receiver)
            .Where(r => r.SenderId == userId && r.Status == "Pending")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetConnectionsAsync(int userId)
    {
        var accepted = await _context.ConnectionRequests
            .Include(r => r.Sender)
            .Include(r => r.Receiver)
            .Where(r => r.Status == "Accepted" &&
                        (r.SenderId == userId || r.ReceiverId == userId))
            .ToListAsync();

        return accepted.Select(r => r.SenderId == userId ? r.Receiver : r.Sender);
    }

    public async Task<IEnumerable<User>> GetDiscoverableUsersAsync(int userId)
    {
        // Users who have no connection request (any status) with the current user
        var relatedUserIds = await _context.ConnectionRequests
            .Where(r => r.SenderId == userId || r.ReceiverId == userId)
            .Select(r => r.SenderId == userId ? r.ReceiverId : r.SenderId)
            .ToListAsync();

        relatedUserIds.Add(userId); // exclude self

        return await _context.Users
            .Where(u => !relatedUserIds.Contains(u.Id))
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task AcceptAsync(int requestId)
    {
        var request = await _context.ConnectionRequests.FindAsync(requestId);
        if (request != null)
        {
            request.Status = "Accepted";
            await _context.SaveChangesAsync();
        }
    }

    public async Task RejectAsync(int requestId)
    {
        var request = await _context.ConnectionRequests.FindAsync(requestId);
        if (request != null)
        {
            request.Status = "Rejected";
            await _context.SaveChangesAsync();
        }
    }
}
