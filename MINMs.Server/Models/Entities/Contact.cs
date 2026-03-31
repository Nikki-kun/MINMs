namespace MINMs.Server.Models.Entities;

public sealed class Contact
{
    public int ContactRowId { get; set; }
    public int OwnerId { get; set; }
    public int ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public DateTime ContactAddedAt { get; set; }
}
