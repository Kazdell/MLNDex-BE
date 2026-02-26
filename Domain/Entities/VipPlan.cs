using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class VipPlan
	{
		public int PlanId { get; set; }
		public string Name { get; set; } = null!;
		public string? Description { get; set; }
		public decimal PriceVnd { get; set; }
		public int DurationDays { get; set; }
		public bool AutoUnlockChapter { get; set; }
		public bool IsActive { get; set; }

		// Navigation
		public ICollection<VipSubscription> VipSubscriptions { get; set; } = new List<VipSubscription>();
	}
}
