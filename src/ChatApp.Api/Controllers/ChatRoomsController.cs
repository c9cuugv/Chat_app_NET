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

    public ChatRoomsController(IChatRoomRepository roomRepository)
    {
        _roomRepository = roomRepository;
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
        
        var room = await _roomRepository.GetPrivateRoomAsync(currentUserId, otherUserId);
        
        if (room == null)
        {
            room = new ChatRoom
            {
                Name = $"Private Chat",
                Type = "Private",
                CreatedAt = DateTime.UtcNow
            };
            
            room = await _roomRepository.CreateAsync(room);
            await _roomRepository.AddUserToRoomAsync(room.Id, currentUserId);
            await _roomRepository.AddUserToRoomAsync(room.Id, otherUserId);
        }
        
        return Ok(room);
    }
}
