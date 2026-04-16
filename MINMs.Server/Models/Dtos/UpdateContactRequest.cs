using System.ComponentModel.DataAnnotations;

namespace MINMs.Server.Models.Dtos;

public sealed class UpdateContactRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string ContactName { get; set; } = string.Empty;
}
