using System;

namespace Domain.Entities
{
  public class TeamJoinRequest
  {
    public int RequestId { get; set; }
    public int TeamId { get; set; }
    public int UserId { get; set; }
    public string Message { get; set; } = null!;
    public TeamJoinRequestStatus Status { get; set; } = TeamJoinRequestStatus.PENDING;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public int? RespondedBy { get; set; }

    // Navigation
    public TranslationTeam Team { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? RespondedByUser { get; set; }
  }

  public enum TeamJoinRequestStatus
  {
    PENDING,
    APPROVED,
    REJECTED
  }
}
