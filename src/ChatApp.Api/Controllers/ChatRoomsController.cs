using System.Security.Claims;
using ChatApp.Core.Entities;
using ChatApp.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatRoomsController : ControllerBase
{
    private readonly IChatRoomRepository _roomRepository;
    private readonly IConnectionRepository _connectionRepository;

    public ChatRoomsController(IChatRoomRepository roomRepository, IConnectionRepository connectionRepository)
    {
        _roomRepository = roomRepository;
        _connectionRepository = connectionRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ChatRoom>>> GetMyRooms()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var rooms = await _roomRepository.GetUserRoomsAsync(userId);
        return Ok(rooms);
    }

    [HttpPost("private/{otherUserId}")]
    public async Task<ActionResult<ChatRoom>> GetOrCreatePrivateRoom(int otherUserId)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        if (currentUserId == otherUserId)
            return BadRequest(new { message = "Cannot create a chat room with yourself." });

        var room = await _roomRepository.GetOrCreatePrivateRoomAsync(currentUserId, otherUserId);
        return Ok(room);
    }

    [HttpPost("group")]
    public async Task<ActionResult<ChatRoom>> CreateGroupChannel([FromBody] CreateGroupDto dto)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Channel name is required." });

        if (dto.MemberIds == null || dto.MemberIds.Count == 0)
            return BadRequest(new { message = "At least one member is required." });

        // Only allow adding existing connections
        var connections = await _connectionRepository.GetConnectionsAsync(currentUserId);
        var connectionIds = connections.Select(u => u.Id).ToHashSet();
        var invalidMembers = dto.MemberIds.Where(id => !connectionIds.Contains(id)).ToList();
        if (invalidMembers.Count != 0)
            return BadRequest(new { message = "You can only add your connects to a channel." });

        var room = new ChatRoom
        {
            Name = dto.Name.Trim(),
            Type = "Group",
            CreatedAt = DateTime.UtcNow
        };

        var created = await _roomRepository.CreateAsync(room);

        // Add creator + all selected members
        await _roomRepository.AddUserToRoomAsync(created.Id, currentUserId);
        foreach (var memberId in dto.MemberIds.Distinct())
            await _roomRepository.AddUserToRoomAsync(created.Id, memberId);

        // Return with participants populated
        var full = await _roomRepository.GetByIdAsync(created.Id);
        return Ok(full);
    }
}

public record CreateGroupDto(string Name, List<int> MemberIds);
