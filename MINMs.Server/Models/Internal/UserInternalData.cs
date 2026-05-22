namespace MINMs.Server.Models.Internal;

public sealed class UserInternalData
{
    public int UserId { get; init; }
    public string Login { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public int? AvatarId { get; init; }
    public DateTime UserCreatedAt { get; init; }
}
