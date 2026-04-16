namespace MINMs.Server.Models.Internal;

/// <summary>
/// Полная внутренняя модель пользователя для backend-сценариев.
/// Не предназначена для публичных API-ответов.
/// </summary>
public sealed class UserInternalData
{
    public int UserId { get; init; }
    public string Login { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public DateTime UserCreatedAt { get; init; }
}
