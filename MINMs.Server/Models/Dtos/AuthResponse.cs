namespace MINMs.Server.Models.Dtos;

/// <summary>Ответ после успешной регистрации или входа: JWT и публичные поля пользователя.</summary>
public sealed class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public string Login { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime UserCreatedAt { get; set; }
}
