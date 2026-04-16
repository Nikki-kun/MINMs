using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MINMs.Server.Models.Dtos;
using MINMs.Server.Services;

namespace MINMs.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public sealed class ContactsController(IContactService contactService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ContactPublicDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ContactPublicDto>>> GetMyContacts(CancellationToken cancellationToken)
    {
        var ownerId = TryGetCurrentUserId();
        if (ownerId is null)
            return Unauthorized();

        var contacts = await contactService.GetContactsAsync(ownerId.Value, cancellationToken).ConfigureAwait(false);
        return Ok(contacts);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Add(
        [FromBody] AddContactRequest request,
        CancellationToken cancellationToken)
    {
        var ownerId = TryGetCurrentUserId();
        if (ownerId is null)
            return Unauthorized();

        var result = await contactService.AddContactAsync(ownerId.Value, request.Login, request.ContactName, cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            AddContactResult.Success => StatusCode(StatusCodes.Status201Created),
            AddContactResult.AlreadyExists => Conflict(new { message = "Контакт уже добавлен." }),
            AddContactResult.ContactNotFound => NotFound(new { message = "Пользователь не найден." }),
            AddContactResult.CannotAddSelf => BadRequest(new { message = "Нельзя добавить самого себя." }),
            AddContactResult.InvalidInput => BadRequest(new { message = "Некорректные данные контакта." }),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPut("{login}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
        [FromRoute] string login,
        [FromBody] UpdateContactRequest request,
        CancellationToken cancellationToken)
    {
        var ownerId = TryGetCurrentUserId();
        if (ownerId is null)
            return Unauthorized();

        var result = await contactService.UpdateContactAsync(ownerId.Value, login, request.ContactName, cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            UpdateContactResult.Success => Ok(),
            UpdateContactResult.ContactNotFound => NotFound(new { message = "Контакт не найден." }),
            UpdateContactResult.InvalidInput => BadRequest(new { message = "Некорректные данные контакта." }),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpDelete("{login}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
        [FromRoute] string login,
        CancellationToken cancellationToken)
    {
        var ownerId = TryGetCurrentUserId();
        if (ownerId is null)
            return Unauthorized();

        var result = await contactService.DeleteContactAsync(ownerId.Value, login, cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            DeleteContactResult.Success => Ok(),
            DeleteContactResult.ContactNotFound => NotFound(new { message = "Контакт не найден." }),
            DeleteContactResult.InvalidInput => BadRequest(new { message = "Некорректные данные контакта." }),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private int? TryGetCurrentUserId()
    {
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdRaw, out var ownerId) && ownerId > 0 ? ownerId : null;
    }
}
