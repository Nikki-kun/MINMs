namespace MINMs.Server.Models.Dtos;

public sealed class UserPublicDto
{
    public string Login { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public DateTime UserCreatedAt { get; init; }
}
