using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class UserRole
	{
		public int UserRoleId { get; set; }
		public int UserId { get; set; }
		public int RoleId { get; set; }
		public DateTime AssignedAt { get; set; }

		// Navigation
		public User User { get; set; } = null!;
		public Role Role { get; set; } = null!;
	}
}
