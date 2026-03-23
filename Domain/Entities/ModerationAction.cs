using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
  public class ModerationAction
  {
    public int ActionId { get; set; }
    public int QueueId { get; set; }
    public int ModeratorId { get; set; }
    public ModerationActionType Action { get; set; }
    public string Reason { get; set; } = null!;
    public DateTime ActedAt { get; set; }

    // Navigation
    public ModerationQueue Queue { get; set; } = null!;
    public User Moderator { get; set; } = null!;
  }

  public enum ModerationActionType
  {
    /// <summary>Content is clean, publish immediately.</summary>
    AutoPass,

    /// <summary>Content is suspicious, queue for human moderator review.</summary>
    FlagForReview,

    /// <summary>Content clearly violates policy, reject automatically.</summary>
    AutoReject,

    /// <summary>Content triggers zero-tolerance rule (CSAM), ban user instantly.</summary>
    InstantBan
  }
}
