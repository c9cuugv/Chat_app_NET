using System.Security.Claims;
using ChatApp.Api.DTOs;
using ChatApp.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPresenceService _presenceService;

    public UsersController(IUserRepository userRepository, IPresenceService presenceService)
    {
        _userRepository = userRepository;
        _presenceService = presenceService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            IsOnline = await _presenceService.IsUserOnlineAsync(user.Id),
            CreatedAt = user.CreatedAt
        });
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
    {
        var users = await _userRepository.GetAllAsync();
        var onlineUsers = await _presenceService.GetOnlineUsersAsync();
        var onlineUserIds = onlineUsers.Select(int.Parse).ToHashSet();

        var dtos = users.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            IsOnline = onlineUserIds.Contains(u.Id),
            CreatedAt = u.CreatedAt
        });
        return Ok(dtos);
    }
}
