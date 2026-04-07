using Domain.Entities;
using System.Collections.Generic;

namespace Application.DTOs.Translation.Requests
{
  // Create a new translation team
  public class CreateTranslationTeamRequest
  {
    public string TeamName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int LanguageId { get; set; } = 1;
    public bool RequireApproval { get; set; } = true;
    public string? AvatarUrl { get; set; }
    public string? BannerUrl { get; set; }
    public List<int>? GenreIds { get; set; } = new();
    public string? Facebook { get; set; }
    public string? Discord { get; set; }
    public string? Website { get; set; }
    public string? Certificates { get; set; }
  }

  // Update an existing translation team
  public class UpdateTranslationTeamRequest
  {
    public string? TeamName { get; set; }
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public int? LanguageId { get; set; }
    public bool? RequireApproval { get; set; }
    public string? AvatarUrl { get; set; }
    public string? BannerUrl { get; set; }
    public List<int>? GenreIds { get; set; }
    public string? Facebook { get; set; }
    public string? Discord { get; set; }
    public string? Website { get; set; }
    public string? Certificates { get; set; }

    // ── Unlock Settings ──
    public bool? UnlockEnabled { get; set; }
    public int? DefaultUnlockPriceCoins { get; set; }
    public bool? FreeAfterEnabled { get; set; }
    public int? DefaultFreeAfterDays { get; set; }
  }

  // Invite a member to the team
  public class InviteTeamMemberRequest
  {
    public int UserId { get; set; }
    public TeamMemberRole Role { get; set; } = TeamMemberRole.TRANSLATOR;
  }

  // Assign role to a team member
  public class AssignTeamMemberRoleRequest
  {
    public TeamMemberRole Role { get; set; }
  }

  // Request to join a team
  public class JoinTeamRequest
  {
    public string Message { get; set; } = string.Empty;
  }
}
