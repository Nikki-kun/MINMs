namespace MINMs.Server.Models.Entities;

public sealed class Message
{
    public int MessageId { get; set; }
    public int SenderId { get; set; }
    public int ChatId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime MessageCreatedAt { get; set; }
    public byte Status { get; set; }
    public byte Type { get; set; }
}
