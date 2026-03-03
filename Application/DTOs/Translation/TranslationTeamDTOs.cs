using System;
using System.Collections.Generic;
using Domain.Entities;

namespace Application.DTOs.Translation
{
    // Request Models
    public class CreateTranslationTeamDto
    {
        public string TeamName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class InviteTeamMemberDto
    {
        public int UserId { get; set; }
        public TeamMemberRole Role { get; set; } = TeamMemberRole.TRANSLATOR;
    }

    public class AssignTeamMemberRoleDto
    {
        public TeamMemberRole Role { get; set; }
    }

    // Response Models
    public class TranslationTeamDto
    {
        public int TeamId { get; set; }
        public int LeaderId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ReputationScore { get; set; }
        public string LockStatus { get; set; } = string.Empty;
        public bool IsMonetizationEnabled { get; set; }
        public string ModerationStatus { get; set; } = string.Empty;
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
}
