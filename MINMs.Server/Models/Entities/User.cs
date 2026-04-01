namespace MINMs.Server.Models.Entities;

public sealed class User
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool Online { get; set; }
    public DateTime UserLastSeen { get; set; }
    public DateTime UserCreatedAt { get; set; }
}
