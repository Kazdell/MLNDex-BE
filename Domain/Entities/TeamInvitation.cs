using System;

namespace Domain.Entities
{
  public class TeamInvitation
  {
    public int InvitationId { get; set; }
    public int TeamId { get; set; }
    public int InviteeId { get; set; }
    public int InviterId { get; set; }
    public string Role { get; set; } = "MEMBER";
    public TeamInvitationStatus Status { get; set; } = TeamInvitationStatus.PENDING;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public string? Note { get; set; }

    // Navigation
    public TranslationTeam Team { get; set; } = null!;
    public User Invitee { get; set; } = null!;
    public User Inviter { get; set; } = null!;
  }

  public enum TeamInvitationStatus
  {
    PENDING,
    ACCEPTED,
    REJECTED,
    EXPIRED
  }
}
