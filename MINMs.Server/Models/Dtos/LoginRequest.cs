using System.ComponentModel.DataAnnotations;

namespace MINMs.Server.Models.Dtos;

public sealed class LoginRequest
{
    [Required]
    [StringLength(40, MinimumLength = 1)]
    public string Login { get; set; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}
