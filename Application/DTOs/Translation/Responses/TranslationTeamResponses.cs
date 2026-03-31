using System;
using System.Collections.Generic;

namespace Application.DTOs.Translation.Responses
{
  // Full team details response
  public class TranslationTeamResponse
  {
    public int TeamId { get; set; }
    public int LeaderId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int LanguageId { get; set; }
    public bool RequireApproval { get; set; }
    public int ReputationScore { get; set; }
    public string LockStatus { get; set; } = string.Empty;
    public bool IsMonetizationEnabled { get; set; }
    public string ModerationStatus { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? BannerUrl { get; set; }
    public List<string>? Genres { get; set; }
    public string? Facebook { get; set; }
    public string? Discord { get; set; }
    public string? Website { get; set; }
    public string? Certificates { get; set; }
    public int MemberCount { get; set; }
    public string Role { get; set; } = string.Empty;

    // ── Unlock Settings ──
    public bool? UnlockEnabled { get; set; }
    public int? DefaultUnlockPriceCoins { get; set; }
    public bool? FreeAfterEnabled { get; set; }
    public int? DefaultFreeAfterDays { get; set; }
  }

  // Team member summary response
  public class TeamMemberResponse
  {
    public int MembershipId { get; set; }
    public int TeamId { get; set; }
    public int UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; }
  }

  // Detailed member info (includes user display data)
  public class TeamMemberDetailResponse
  {
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
  }

  // Invitation response
  public class TeamInvitationResponse
  {
    public int InvitationId { get; set; }
    public int TeamId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string TargetRole { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime InvitedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
  }

  // Join request response
  public class TeamJoinRequestResponse
  {
    public int RequestId { get; set; }
    public int TeamId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
  }
}
