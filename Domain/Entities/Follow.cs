using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class Follow
	{
		public int FollowId { get; set; }
		public int UserId { get; set; }
		public int TargetId { get; set; }
		public FollowTargetType TargetType { get; set; }
		public DateTime FollowedAt { get; set; }

		// Navigation
		public User User { get; set; } = null!;
	}

	public enum FollowTargetType
	{
		SERIES,
		CREATOR,
		TEAM
	}
}
