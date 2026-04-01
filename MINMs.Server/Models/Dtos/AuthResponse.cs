namespace MINMs.Server.Models.Dtos;

public sealed class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public int UserId { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime UserCreatedAt { get; set; }
}
