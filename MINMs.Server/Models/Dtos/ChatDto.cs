namespace MINMs.Server.Models.Dtos;

public class ChatDto
{
    public int ChatId { get; set; }
    public ChatType Type { get; set; }
    public DateTime ChatCreatedAt { get; set; }
    public List<ChatParticipantDto> Participants { get; set; } = [];
}

public class ChatParticipantDto
{
    public int UserId { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int? AvatarId { get; set; }
    public ParticipantRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class ChatPreviewDto
{
    public int ChatId { get; set; }
    public ChatType Type { get; set; }
    public DateTime ChatCreatedAt { get; set; }
    public string? LastMessageContent { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
}

public class CreateChatRequest
{
    public ChatType Type { get; set; }
    public List<int>? ParticipantIds { get; set; }
}

public enum ChatType : sbyte
{
    Personal = 1,
    Group = 2
}

public enum ParticipantRole : sbyte
{
    Owner = 1,
    Member = 2
}

public enum MembershipStatus : sbyte
{
    Active = 0,
    Left = 1,
    Banned = 2
}
