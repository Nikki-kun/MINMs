namespace MINMs.Server.Models.Dtos;

public sealed class ContactPublicDto
{
    public string Login { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string ContactName { get; init; } = string.Empty;
    public DateTime ContactAddedAt { get; init; }
}

