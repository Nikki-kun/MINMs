namespace MINMs.Server.Models.Entities;

public sealed class BlockedUser
{
    public int UserId { get; set; }
    public int BlockedUserId { get; set; }
    public DateTime BlockedAt { get; set; }
}
