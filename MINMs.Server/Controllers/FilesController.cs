using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Файл не выбран.");

        var objectName = $"{Guid.NewGuid()}_{file.FileName}";

        var (success, error) = await _storageService.UploadFileAsync(file, objectName);
        if (!success)
            return StatusCode(500, error ?? "Ошибка загрузки в MinIO.");

        return Ok(new { Message = "Файл загружен", ObjectName = objectName });
    }

    /// <summary>Редирект на временную presigned-ссылку для скачивания объекта.</summary>
    [HttpGet("download")]
    public async Task<IActionResult> Download(string objectName)
    {
        var url = await _storageService.GetFileUrlAsync(objectName);
        if (string.IsNullOrEmpty(url))
            return NotFound("Файл не найден.");

        return Redirect(url);
    }
}
