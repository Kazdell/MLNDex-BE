using System;
using System.Collections.Generic;

namespace Domain.Entities
{
	public class TranslationTeam
	{
		public int TeamId { get; set; }
		public int LeaderId { get; set; }
		public string TeamName { get; set; } = null!;
		public string? Slug { get; set; }
		public string? Description { get; set; }
		public int LanguageId { get; set; } = 1;
		public bool RequireApproval { get; set; } = true;
		public int ReputationScore { get; set; }
		public int TrustScore { get; set; } = 100;
		public TeamLockStatus LockStatus { get; set; }
		public ModerationStatus ModerationStatus { get; set; }
		public bool IsMonetizationEnabled { get; set; }
		public string? AvatarUrl { get; set; }
		public string? BannerUrl { get; set; }
		public string? Facebook { get; set; }
		public string? Discord { get; set; }
		public string? Website { get; set; }
		public string? Certificates { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? UpdatedAt { get; set; }
		public DateTime? LockedAt { get; set; }
		public int? LockedBy { get; set; }

		// ── Unlock Settings ──────────────────────────────────────
		public bool UnlockEnabled { get; set; } = false;
		public int? DefaultUnlockPriceCoins { get; set; }
		public bool FreeAfterEnabled { get; set; } = false;
		public int? DefaultFreeAfterDays { get; set; }

		// Navigation
		public Language Language { get; set; } = null!;
		public User Leader { get; set; } = null!;
		public User? LockedByUser { get; set; }
		public ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
		public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
		public ICollection<TranslationPermission> TranslationPermissions { get; set; } = new List<TranslationPermission>();
		public ICollection<Translation> Translations { get; set; } = new List<Translation>();
		public ICollection<TeamGenre> TeamGenres { get; set; } = new List<TeamGenre>();
		public ICollection<TranslationTeamJoin> TeamJoins { get; set; } = new List<TranslationTeamJoin>();
		public ICollection<TrustScoreHistory> TrustScoreHistories { get; set; } = new List<TrustScoreHistory>();
	}

	public enum TeamLockStatus
	{
		ACTIVE,
		LOCKED,
		BANNED,
		DISBANDED
	}
}
