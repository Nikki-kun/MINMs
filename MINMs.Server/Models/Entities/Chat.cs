namespace MINMs.Server.Models.Entities;

public sealed class Chat
{
    public int ChatId { get; set; }
    public byte Type { get; set; }
    public DateTime ChatCreatedAt { get; set; }
}
