using System.ComponentModel.DataAnnotations;

namespace MINMs.Server.Models.Dtos;

public sealed class RegisterRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(40, MinimumLength = 1)]
    public string Login { get; set; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}
