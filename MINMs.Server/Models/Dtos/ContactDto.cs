using System.ComponentModel.DataAnnotations;
namespace MINMs.Server.Models.Dtos;

public sealed class ContactPublicDto
{
    public string Login { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string ContactName { get; init; } = string.Empty;
    public DateTime ContactAddedAt { get; init; }
}

public sealed class UpdateContactRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string ContactName { get; set; } = string.Empty;
}

public sealed class AddContactRequest
{
    [Required]
    [StringLength(20, MinimumLength = 2)]
    public string Login { get; set; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 2)]
    public string ContactName { get; set; } = string.Empty;
}
