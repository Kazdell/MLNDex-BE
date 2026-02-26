using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class VipSubscription
	{
		public int SubscriptionId { get; set; }
		public int UserId { get; set; }
		public int PlanId { get; set; }
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		public decimal PricePaid { get; set; }
		public bool AutoRenew { get; set; }
		public SubscriptionStatus Status { get; set; }

		// Navigation
		public User User { get; set; } = null!;
		public VipPlan VipPlan { get; set; } = null!;
	}

	public enum SubscriptionStatus
	{
		ACTIVE,
		EXPIRED,
		CANCELLED
	}
}
