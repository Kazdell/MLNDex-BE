using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class TeamMember
	{
		public int MembershipId { get; set; }
		public int TeamId { get; set; }
		public int UserId { get; set; }
		public TeamMemberRole Role { get; set; }
		public DateTime JoinedAt { get; set; }
		public bool IsActive { get; set; }

		// Navigation
		public TranslationTeam TranslationTeam { get; set; } = null!;
		public User User { get; set; } = null!;
	}

	public enum TeamMemberRole
	{
		LEADER,
		TRANSLATOR,
		EDITOR,
		PROOFREADER
	}
}
