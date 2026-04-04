using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MINMs.Server.Models.Dtos;
using MINMs.Server.Services;

namespace MINMs.Server.Controllers;

/// <summary>
/// Регистрация, вход и профиль текущего пользователя.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(AuthService authService, UserSearchService userSearchService) : ControllerBase
{
    /// <summary>Профиль по JWT (требуется заголовок Authorization: Bearer).</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserPublicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserPublicDto>> Me(CancellationToken cancellationToken)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (sub is null || !int.TryParse(sub, out var userId))
            return Unauthorized();

        var profile = await userSearchService.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>Создание учётной записи и выдача JWT при успехе.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await authService.RegisterAsync(request, cancellationToken).ConfigureAwait(false);
        return outcome.Kind switch
        {
            RegisterOutcomeKind.Created when outcome.Response is not null =>
                StatusCode(StatusCodes.Status201Created, outcome.Response),
            RegisterOutcomeKind.DuplicateLogin =>
                Conflict(new { message = "Этот логин уже занят." }),
            RegisterOutcomeKind.InvalidLogin =>
                BadRequest(new { message = "Логин: 5–32 символа, латиница, цифры и подчёркивание, без пробелов." }),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>Проверка логина и пароля; при успехе возвращает JWT и данные пользователя.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken).ConfigureAwait(false);
        if (response is null)
            return Unauthorized(new { message = "Неверный логин или пароль." });

        return Ok(response);
    }
}
