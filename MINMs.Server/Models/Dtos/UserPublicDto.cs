namespace MINMs.Server.Models.Dtos;

public sealed class UserPublicDto
{
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public bool Online { get; init; }
    public DateTime UserLastSeen { get; init; }
    public DateTime UserCreatedAt { get; init; }
}
