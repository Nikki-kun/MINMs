using System.ComponentModel.DataAnnotations;

namespace MINMs.Server.Models.Dtos;

/// <summary>Тело запроса <c>POST /api/Auth/register</c></summary>
public sealed class RegisterRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}
