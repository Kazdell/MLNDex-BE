using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class TranslationTeam
	{
		public int TeamId { get; set; }
		public int LeaderId { get; set; }
		public string TeamName { get; set; } = null!;
		public string Slug { get; set; } = null!;
		public string? Description { get; set; }
		public string Language { get; set; } = "Tiếng Việt";
		public bool RequireApproval { get; set; } = true;
		public int ReputationScore { get; set; }
		public TeamLockStatus LockStatus { get; set; }
		public bool IsMonetizationEnabled { get; set; }
		public int? LockedBy { get; set; }
		public DateTime? LockedAt { get; set; }
		public ModerationStatus ModerationStatus { get; set; }
		public string? AvatarUrl { get; set; }
		public string? BannerUrl { get; set; }

		// Social links
		public string? Facebook { get; set; }
		public string? Discord { get; set; }
		public string? Website { get; set; }

		// Navigation
		public User Leader { get; set; } = null!;
		public User? LockedByUser { get; set; }
		public ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
		public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
		public ICollection<TranslationPermission> TranslationPermissions { get; set; } = new List<TranslationPermission>();
		public ICollection<TeamGenre> TeamGenres { get; set; } = new List<TeamGenre>();
	}

	public enum TeamLockStatus
	{
		ACTIVE,
		LOCKED,
		BANNED
	}
}
