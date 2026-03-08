using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class User
	{
		public int UserId { get; set; }
		public string Username { get; set; } = null!;
		public string Email { get; set; } = null!;
		public string? DisplayName { get; set; } = null!;
		public string? DisplayAvatar { get; set; }
		public string? PasswordHash { get; set; } = null!;
		public bool IsEmailVerified { get; set; } = false; 
		public string? GoogleId { get; set; }              
		public string? FacebookId { get; set; }
		public bool IsActive { get; set; }
		public DateTime CreatedAt { get; set; }

		// Navigation
		public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
		public CreatorProfile? CreatorProfile { get; set; }
		public Wallet? Wallet { get; set; }
		public ICollection<VipSubscription> VipSubscriptions { get; set; } = new List<VipSubscription>();
		public ICollection<TranslationTeam> LeadingTeams { get; set; } = new List<TranslationTeam>();
		public ICollection<TeamMember> TeamMemberships { get; set; } = new List<TeamMember>();
		public ICollection<ReadingHistory> ReadingHistories { get; set; } = new List<ReadingHistory>();
		public ICollection<Follow> Follows { get; set; } = new List<Follow>();
		public ICollection<Comment> Comments { get; set; } = new List<Comment>();
		public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
		public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
		public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
		public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
		public ICollection<Report> Reports { get; set; } = new List<Report>();
		public ICollection<TranslationTeam> LockedTeams { get; set; } = new List<TranslationTeam>();
		public ICollection<TranslationPermission> GrantedPermissions { get; set; } = new List<TranslationPermission>();
		public ICollection<ModerationQueue> AssignedQueues { get; set; } = new List<ModerationQueue>();
		public ICollection<ModerationAction> ModerationActions { get; set; } = new List<ModerationAction>();
	}
}
