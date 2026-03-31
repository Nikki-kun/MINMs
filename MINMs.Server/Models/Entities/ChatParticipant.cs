namespace MINMs.Server.Models.Entities;

public sealed class ChatParticipant
{
    public int ChatId { get; set; }
    public int UserId { get; set; }
    public byte ParticipantRole { get; set; }
    public byte MembershipStatus { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public DateTime? BannedAt { get; set; }
}
