using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MINMs.Server.Models.Dtos;
using MINMs.Server.Services;

namespace MINMs.Server.Controllers;

/// <summary>
/// Поиск пользователей
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class UsersController(IUserSearchService userSearchService) : ControllerBase
{
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<UserPublicDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserPublicDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var results = await userSearchService.SearchByUsernameAsync(q ?? string.Empty, limit, cancellationToken)
            .ConfigureAwait(false);
        return Ok(results);
    }
}
