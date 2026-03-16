using System;
using System.Collections.Generic;
using Domain.Entities;

namespace Application.DTOs.Translation
{
    // Request Models
    public class CreateTranslationTeamDto
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

    public class InviteTeamMemberDto
    {
        public int UserId { get; set; }
        public TeamMemberRole Role { get; set; } = TeamMemberRole.TRANSLATOR;
    }

    public class UpdateTranslationTeamDto
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
    }

    public class AssignTeamMemberRoleDto
    {
        public TeamMemberRole Role { get; set; }
    }

    public class JoinTeamRequestDto
    {
        public string Message { get; set; } = string.Empty;
    }

    // Response Models
    public class TranslationTeamDto
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
    }

    public class TeamMemberDto
    {
        public int MembershipId { get; set; }
        public int TeamId { get; set; }
        public int UserId { get; set; }
        public string Role { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class TeamMemberDetailDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
    }

    public class TeamInvitationDto
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

    public class TeamJoinRequestDtoResponse
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
