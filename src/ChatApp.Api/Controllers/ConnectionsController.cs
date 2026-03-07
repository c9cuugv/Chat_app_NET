using System.Security.Claims;
using ChatApp.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConnectionsController : ControllerBase
{
    private readonly IConnectionRepository _connections;

    public ConnectionsController(IConnectionRepository connections)
    {
        _connections = connections;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    // GET /api/connections — accepted connects
    [HttpGet]
    public async Task<IActionResult> GetConnections()
    {
        var users = await _connections.GetConnectionsAsync(CurrentUserId);
        var dtos = users.Select(u => new { u.Id, u.Username });
        return Ok(dtos);
    }

    // GET /api/connections/discover — users with no relation yet
    [HttpGet("discover")]
    public async Task<IActionResult> Discover()
    {
        var users = await _connections.GetDiscoverableUsersAsync(CurrentUserId);
        var dtos = users.Select(u => new { u.Id, u.Username });
        return Ok(dtos);
    }

    // GET /api/connections/pending — received + sent pending requests
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var received = await _connections.GetPendingReceivedAsync(CurrentUserId);
        var sent = await _connections.GetPendingSentAsync(CurrentUserId);

        return Ok(new
        {
            received = received.Select(r => new
            {
                r.Id,
                senderId = r.SenderId,
                senderUsername = r.Sender.Username,
                r.CreatedAt
            }),
            sent = sent.Select(r => new
            {
                r.Id,
                receiverId = r.ReceiverId,
                receiverUsername = r.Receiver.Username,
                r.CreatedAt
            })
        });
    }

    // POST /api/connections/send/{targetUserId} — send a request
    [HttpPost("send/{targetUserId:int}")]
    public async Task<IActionResult> SendRequest(int targetUserId)
    {
        var me = CurrentUserId;
        if (me == targetUserId)
            return BadRequest(new { message = "Cannot connect with yourself." });

        var existing = await _connections.GetRequestBetweenAsync(me, targetUserId);
        if (existing != null)
            return Conflict(new { message = "A request already exists.", status = existing.Status });

        var request = await _connections.SendRequestAsync(me, targetUserId);
        return Ok(new { request.Id, request.Status });
    }

    // PUT /api/connections/{requestId}/accept
    [HttpPut("{requestId:int}/accept")]
    public async Task<IActionResult> Accept(int requestId)
    {
        var request = await _connections.GetRequestAsync(requestId);
        if (request == null) return NotFound();
        if (request.ReceiverId != CurrentUserId) return Forbid();
        if (request.Status != "Pending") return BadRequest(new { message = "Request is not pending." });

        await _connections.AcceptAsync(requestId);
        return Ok(new { message = "Connection accepted." });
    }

    // PUT /api/connections/{requestId}/reject
    [HttpPut("{requestId:int}/reject")]
    public async Task<IActionResult> Reject(int requestId)
    {
        var request = await _connections.GetRequestAsync(requestId);
        if (request == null) return NotFound();
        if (request.ReceiverId != CurrentUserId) return Forbid();
        if (request.Status != "Pending") return BadRequest(new { message = "Request is not pending." });

        await _connections.RejectAsync(requestId);
        return Ok(new { message = "Request rejected." });
    }
}
