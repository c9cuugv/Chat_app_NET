using ChatApp.Api.DTOs;
using ChatApp.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var (user, error) = await _authService.RegisterAsync(dto.Username, dto.Email, dto.Password);
        if (user == null)
        {
            return BadRequest(new { message = error });
        }
        return Ok(new { message = "User registered successfully", userId = user.Id });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var (user, token) = await _authService.LoginAsync(dto.Email, dto.Password);
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }
        return Ok(new { token, userId = user.Id, username = user.Username });
    }
}
