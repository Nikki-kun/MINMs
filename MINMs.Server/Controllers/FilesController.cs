using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MINMs.Server.Models.Dtos;
using MINMs.Server.Services;

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
    [ProducesResponseType(typeof(FileUploadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("The file is not selected");

        var objectName = $"{Guid.NewGuid()}_{file.FileName}";

        var (success, error) = await _storageService.UploadFileAsync(file, objectName);
        if (!success)
            return StatusCode(500, error ?? "Error loading to the storage");

        return Ok(new FileUploadResponseDto
        {
            Message = "The file is uploaded",
            ObjectName = objectName,
        });
    }

    /// <summary>Редирект на временную presigned-ссылку для скачивания объекта.</summary>
    [HttpGet("download")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(string objectName)
    {
        var url = await _storageService.GetFileUrlAsync(objectName);
        if (string.IsNullOrEmpty(url))
            return NotFound("The file was not found");

        return Redirect(url);
    }
}
