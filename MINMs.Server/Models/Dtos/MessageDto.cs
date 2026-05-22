namespace MINMs.Server.Models.Dtos;

public class MessageDto
{
    public int MessageId { get; set; }
    public int SenderId { get; set; }
    public string SenderLogin { get; set; } = string.Empty;
    public string SenderUsername { get; set; } = string.Empty;
    public int ChatId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime MessageCreatedAt { get; set; }
    public MessageStatus Status { get; set; }
    public MessageType Type { get; set; }
}

public enum MessageStatus : sbyte
{
    Sent = 0,
    Delivered = 1,
    Read = 2,
    Deleted = 3
}

public enum MessageType : sbyte
{
    Text = 0,
    Image = 1,
    File = 2,
    System = 3
}
