// Controllers/ChatsController.cs
using Microsoft.AspNetCore.Mvc;
using MINMs.Server.Models.Dtos;
using MINMs.Server.Services;

namespace MINMs.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ChatsController(IChatService chatService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ChatDto>> CreateChat(
        [FromBody] CreateChatRequest request,
        [FromHeader(Name = "X-User-Id")] int userId, // или из токена
        CancellationToken cancellationToken)
    {
        var result = await chatService.CreateChatAsync(request, userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChatPreviewDto>>> GetMyChats(
        [FromHeader(Name = "X-User-Id")] int userId,
        CancellationToken cancellationToken)
    {
        var chats = await chatService.GetUserChatsAsync(userId, cancellationToken);
        return Ok(chats);
    }

    [HttpGet("{chatId:int}")]
    public async Task<ActionResult<ChatDto>> GetChat(
        int chatId,
        [FromHeader(Name = "X-User-Id")] int userId,
        CancellationToken cancellationToken)
    {
        var chat = await chatService.GetChatByIdAsync(chatId, userId, cancellationToken);
        return chat is null ? NotFound() : Ok(chat);
    }

    [HttpDelete("{chatId:int}")]
    public async Task<IActionResult> DeleteChat(
        int chatId,
        [FromHeader(Name = "X-User-Id")] int userId,
        CancellationToken cancellationToken)
    {
        var deleted = await chatService.DeleteChatAsync(chatId, userId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{chatId:int}/participants/{targetUserId:int}")]
    public async Task<IActionResult> AddParticipant(
        int chatId,
        int targetUserId,
        [FromHeader(Name = "X-User-Id")] int ownerUserId,
        CancellationToken cancellationToken)
    {
        var added = await chatService.AddParticipantAsync(chatId, ownerUserId, targetUserId, cancellationToken);
        return added ? Ok() : BadRequest("Failed to add participant");
    }

    [HttpDelete("{chatId:int}/participants/{targetUserId:int}")]
    public async Task<IActionResult> RemoveParticipant(
        int chatId,
        int targetUserId,
        [FromHeader(Name = "X-User-Id")] int ownerUserId,
        CancellationToken cancellationToken)
    {
        var removed = await chatService.RemoveParticipantAsync(chatId, ownerUserId, targetUserId, cancellationToken);
        return removed ? NoContent() : BadRequest("Failed to remove participant");
    }

    [HttpPost("{chatId:int}/leave")]
    public async Task<IActionResult> LeaveChat(
        int chatId,
        [FromHeader(Name = "X-User-Id")] int userId,
        CancellationToken cancellationToken)
    {
        var left = await chatService.LeaveChatAsync(chatId, userId, cancellationToken);
        return left ? NoContent() : BadRequest("Failed to leave chat");
    }
}
