namespace MINMs.Server.Models.Dtos;

public class FileUploadResponseDto
{
    public string Message { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public int? FileId { get; set; }
}
