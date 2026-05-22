using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MINMs.Server.Models.Dtos;
using MINMs.Server.Services;
using System.Security.Claims;

namespace MINMs.Server.Controllers;

/// <summary>
/// Загрузка и выдача ссылок на объекты в MinIO.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FilesController(IMinioStorageService storageService) : ControllerBase
{
    private readonly IMinioStorageService _storageService = storageService;

    /// <summary>Загружает файл в бакет; имя объекта генерируется на сервере.</summary>
    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FileUploadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("The file is not selected");

        var objectName = $"{Guid.NewGuid()}_{file.FileName}";

        var login = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(login))
            return Unauthorized();

        var (success, error, fileId) = await _storageService.UploadFileAsync(login, file, objectName, file.FileName);
        if (!success)
            return StatusCode(500, error ?? "Error loading to the storage");

        return Ok(new FileUploadResponseDto
        {
            Message = "The file is uploaded",
            ObjectName = objectName,
            FileId = fileId
        });
    }

    /// <summary>Редирект на временную presigned-ссылку для скачивания объекта.</summary>
    [HttpGet("download")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(string objectName)
    {
        var url = await _storageService.GetFileUrlAsync(objectName);
        if (string.IsNullOrEmpty(url))
            return NotFound("The file was not found");

        return Redirect(url);
    }

    /// <summary>Удаляет файл по ID.</summary>
    [HttpDelete("{fileId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(int fileId, [FromQuery] string objectName)
    {
        var login = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(login))
            return Unauthorized();

        var success = await _storageService.DeleteFileAsync(login, objectName, fileId);
        if (!success)
            return StatusCode(500, "Error deleting the file");

        return Ok(new { Message = "The file is deleted" });
    }

    [HttpGet("user/files")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<FileResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserFiles()
    {
        var login = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(login))
            return Unauthorized();

        var files = await _storageService.GetUserFilesAsync(login);
        return Ok(files);
    }
}
