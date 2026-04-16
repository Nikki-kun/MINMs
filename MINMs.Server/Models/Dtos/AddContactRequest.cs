using System.ComponentModel.DataAnnotations;

namespace MINMs.Server.Models.Dtos;

public sealed class AddContactRequest
{
    [Required]
    [StringLength(20, MinimumLength = 2)]
    public string Login { get; set; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 2)]
    public string ContactName { get; set; } = string.Empty;
}
