namespace MINMs.Server.Models.Dtos;

public class FileResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public long Size { get; set; }
    public string UploadedAt { get; set; } = string.Empty;
}
