using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MINMs.Server.Services;

namespace MINMs.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FilesController(IMinioStorageService storageService) : ControllerBase
{
    private readonly IMinioStorageService _storageService = storageService;

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

    [HttpGet("download")]
    public async Task<IActionResult> Download(string objectName)
    {
        var url = await _storageService.GetFileUrlAsync(objectName);
        if (string.IsNullOrEmpty(url))
            return NotFound("Файл не найден.");

        return Redirect(url);
    }
}
