using System.Security.Claims;
using ChatApp.Core.Entities;
using ChatApp.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly IMessageRepository _messageRepository;
    private readonly IChatRoomRepository _roomRepository;

    public MessagesController(IMessageRepository messageRepository, IChatRoomRepository roomRepository)
    {
        _messageRepository = messageRepository;
        _roomRepository = roomRepository;
    }

    [HttpGet("room/{roomId}")]
    public async Task<ActionResult<IEnumerable<Message>>> GetRoomMessages(int roomId)
    {
        // Check if user is in room (optional but recommended)
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        if (!await _roomRepository.IsUserInRoomAsync(roomId, userId)) return Forbid();

        var messages = await _messageRepository.GetRecentMessagesAsync(roomId);
        return Ok(messages);
    }
}
