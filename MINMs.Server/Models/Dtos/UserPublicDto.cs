namespace MINMs.Server.Models.Dtos;

public sealed class UserPublicDto
{
    public string Login { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public int? AvatarId { get; init; }
    public DateTime UserCreatedAt { get; init; }
}
