using System.ComponentModel.DataAnnotations;

namespace MINMs.Server.Models.Dtos;

public sealed class LoginRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}
